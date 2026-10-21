using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Credicarro;
using Credicarro.Configuration;
using Credicarro.DependencyInjection;
using Credicarro.Errors;
using Credicarro.Webhooks;

const int MaxWebhookBodyBytes = 1024 * 1024;

var builder = WebApplication.CreateBuilder(args);

var signingSecret = builder.Configuration["Credicarro:WebhookSigningSecret"];

if (string.IsNullOrEmpty(signingSecret))
    throw new InvalidOperationException("Credicarro:WebhookSigningSecret ausente");

var expectedAuthorization = builder.Configuration["Credicarro:WebhookAuthorization"];

builder.Services.AddCredicarro(options =>
{
    options.ClientId = builder.Configuration["Credicarro:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Credicarro:ClientSecret"] ?? string.Empty;
    options.Environment = CredicarroEnvironment.Staging;
});

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxWebhookBodyBytes);

builder.Services.AddSingleton(Channel.CreateBounded<WebhookJob>(1000));
builder.Services.AddSingleton(new ConcurrentDictionary<string, byte>());
builder.Services.AddHostedService<WebhookWorker>();

var app = builder.Build();

app.MapGet("/propostas/{id:guid}", async (Guid id, ICredicarroClient client, CancellationToken cancellationToken) =>
{
    try
    {
        var proposal = await client.Proposals.GetAsync(id, cancellationToken);

        return Results.Ok(new
        {
            proposal.Id,
            proposal.StatusName,
            Pendencias = proposal.GetPendingActions().Select(action => new
            {
                Participante = action.Participant.ToString(),
                Acao = action.Kind.ToString(),
                Documentos = action.DocumentTypes.Select(type => type.ToString())
            })
        });
    }
    catch (CredicarroApiException ex)
    {
        return Results.Problem(detail: ex.Message, statusCode: (int)ex.StatusCode, title: ex.ErrorCode);
    }
});

app.MapPost("/webhooks/credicarro", async (
    HttpRequest request,
    Channel<WebhookJob> queue,
    ConcurrentDictionary<string, byte> seen,
    CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrEmpty(expectedAuthorization)
        && !CredicarroWebhookEvent.VerifyAuthorization(request.Headers.Authorization.ToString(), expectedAuthorization))
    {
        return Results.Unauthorized();
    }

    using var buffer = new MemoryStream();
    var chunk = new byte[8192];
    int read;

    while ((read = await request.Body.ReadAsync(chunk, 0, chunk.Length, cancellationToken)) > 0)
    {
        if (buffer.Length + read > MaxWebhookBodyBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        buffer.Write(chunk, 0, read);
    }

    string rawBody;

    try
    {
        rawBody = new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(buffer.ToArray());
    }
    catch (DecoderFallbackException)
    {
        return Results.BadRequest();
    }

    var signature = request.Headers["X-CrediCarro-Signature"].ToString();

    if (!CredicarroWebhookEvent.VerifySignature(rawBody, signature, signingSecret))
        return Results.Unauthorized();

    Credicarro.Webhooks.Models.ProposalWebhookEvent webhookEvent;

    try
    {
        webhookEvent = CredicarroWebhookEvent.ParseUnverified(rawBody);
    }
    catch (CredicarroWebhookSignatureException)
    {
        return Results.BadRequest();
    }

    var deliveryKey = request.Headers["Idempotency-Key"].ToString();

    if (deliveryKey.Length > 0 && !seen.TryAdd(deliveryKey, 0))
        return Results.Ok();

    if (!queue.Writer.TryWrite(new WebhookJob(webhookEvent.ProposalId)))
    {
        seen.TryRemove(deliveryKey, out _);
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok();
});

app.Run();

internal sealed record WebhookJob(Guid ProposalId);

internal sealed class WebhookWorker(Channel<WebhookJob> queue, ICredicarroClient client, ILogger<WebhookWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                var proposal = await client.Proposals.GetAsync(job.ProposalId, stoppingToken);

                foreach (var action in proposal.GetPendingActions())
                    logger.LogInformation("Webhook | Proposta: {ProposalId} | {Participant} -> {Kind}", proposal.Id, action.Participant, action.Kind);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("Webhook falhou | Proposta: {ProposalId} | Erro: {ErrorType}", job.ProposalId, ex.GetType().Name);
            }
        }
    }
}
