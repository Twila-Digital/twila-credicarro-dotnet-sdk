using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;
using Credicarro;
using Credicarro.Configuration;
using Credicarro.DependencyInjection;
using Credicarro.Errors;
using Credicarro.Webhooks;

const int MaxWebhookBodyBytes = 1024 * 1024; // 1 MiB: o limite de corpo que a Credicarro considera nas entregas.

var builder = WebApplication.CreateBuilder(args);

// O segredo de assinatura é obrigatório: sem ele a aplicação não sobe (nunca aceite webhook sem verificar a assinatura).
var signingSecret = builder.Configuration["Credicarro:WebhookSigningSecret"];

if (string.IsNullOrEmpty(signingSecret))
    throw new InvalidOperationException("Credicarro:WebhookSigningSecret ausente");

// Valor completo do header Authorization cadastrado no webhook ("Basic ..." ou "Bearer ..."), quando houver.
var expectedAuthorization = builder.Configuration["Credicarro:WebhookAuthorization"];

builder.Services.AddCredicarro(options =>
{
    options.ClientId = builder.Configuration["Credicarro:ClientId"] ?? string.Empty;
    options.ClientSecret = builder.Configuration["Credicarro:ClientSecret"] ?? string.Empty;
    options.Environment = CredicarroEnvironment.Staging;
});

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = MaxWebhookBodyBytes);

builder.Services.AddSingleton(Channel.CreateBounded<WebhookJob>(1000)); // fila entre o endpoint e o worker
builder.Services.AddSingleton(new ConcurrentDictionary<string, byte>()); // deduplicação; em produção: Redis ou banco com TTL
builder.Services.AddHostedService<WebhookWorker>();

var app = builder.Build();

app.MapGet("/propostas/{id:guid}", async (Guid id, ICredicarroClient client, CancellationToken cancellationToken) =>
{
    try
    {
        var proposal = await client.Proposals.GetAsync(id, cancellationToken);

        // Só lista o que está pendente; quem envia (veículo, conta, arquivos) é o sistema do integrador, com os próprios dados.
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
    // 1. Authorization (Basic/Jwt) cadastrado no webhook, comparado em tempo constante pelo SDK.
    if (!string.IsNullOrEmpty(expectedAuthorization)
        && !CredicarroWebhookEvent.VerifyAuthorization(request.Headers.Authorization.ToString(), expectedAuthorization))
    {
        return Results.Unauthorized();
    }

    // 2. Corpo bruto, com limite de 1 MiB, antes de qualquer desserialização. O limite é aplicado na leitura,
    //    sem confiar no Content-Length (que o remetente controla e pode nem vir, em chunked).
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

    // 3. Assinatura HMAC sobre o corpo bruto; inválida ou fora da tolerância (5 min): 401 sem detalhes.
    var signature = request.Headers["X-CrediCarro-Signature"].ToString();

    if (!CredicarroWebhookEvent.VerifySignature(rawBody, signature, signingSecret))
        return Results.Unauthorized();

    Credicarro.Webhooks.Models.ProposalWebhookEvent webhookEvent;

    try
    {
        webhookEvent = CredicarroWebhookEvent.ParseUnverified(rawBody); // já verificado acima
    }
    catch (CredicarroWebhookSignatureException)
    {
        return Results.BadRequest();
    }

    // 4. Deduplicação pelo Idempotency-Key da entrega: retentativas repetem a chave.
    var deliveryKey = request.Headers["Idempotency-Key"].ToString();

    if (deliveryKey.Length > 0 && !seen.TryAdd(deliveryKey, 0))
        return Results.Ok(); // entrega repetida

    // 5. TryWrite não espera: fila cheia responde 503 e a Credicarro retenta.
    if (!queue.Writer.TryWrite(new WebhookJob(webhookEvent.ProposalId)))
    {
        seen.TryRemove(deliveryKey, out _); // desfaz a deduplicação para a retentativa
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(); // 2xx só depois de enfileirar
});

app.Run();

internal sealed record WebhookJob(Guid ProposalId);

// Consome a fila: consulta a proposta (as entregas chegam fora de ordem) e age sobre o estado mais recente.
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
                // Só id e tipo do erro (sem payload nem dados pessoais); segue para o próximo job.
                logger.LogError("Webhook falhou | Proposta: {ProposalId} | Erro: {ErrorType}", job.ProposalId, ex.GetType().Name);
            }
        }
    }
}
