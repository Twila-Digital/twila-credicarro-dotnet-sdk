using System.Diagnostics;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using Credicarro.Configuration;
using Credicarro.Http;

namespace Credicarro.Resilience;

internal static class ResiliencePipelineFactory
{
    [ThreadStatic]
    private static Random? _random;

    public static ResiliencePipeline<HttpResponseMessage> Create(CredicarroResilienceOptions options)
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            TimeoutGenerator = args => new ValueTask<TimeSpan>(GetTotalTimeout(args.Context, options))
        });

        if (options.MaxRetryAttempts > 1)
        {
            builder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = options.MaxRetryAttempts - 1,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = false,
                Delay = options.RetryBaseDelay,
                MaxDelay = MaxBudget(options),
                ShouldHandle = args => new ValueTask<bool>(ShouldRetry(args, options)),
                DelayGenerator = args => new ValueTask<TimeSpan?>(GetRetryDelay(args, options)),
                OnRetry = args =>
                {
                    // Libera a conexão da tentativa que será repetida.
                    args.Outcome.Result?.Dispose();
                    return default;
                }
            });
        }

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            FailureRatio = options.CircuitBreakerFailureRatio,
            SamplingDuration = options.CircuitBreakerSamplingDuration,
            MinimumThroughput = options.CircuitBreakerMinimumThroughput,
            BreakDuration = options.CircuitBreakerBreakDuration,
            ShouldHandle = args => new ValueTask<bool>(IsTransientFailure(args.Outcome, options))
        });

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            TimeoutGenerator = args => new ValueTask<TimeSpan>(GetAttemptTimeout(args.Context, options))
        });

        return builder.Build();
    }

    private static bool IsDocumentUpload(ResilienceContext context) =>
        context.Properties.TryGetValue(ResilienceContextRequestKey.Key, out var request)
        && request is not null
        && request.Method == HttpMethod.Post
        && ApiRoutes.IsDocumentUpload(ApiRoutes.GetApiPath(request));

    private static TimeSpan GetAttemptTimeout(ResilienceContext context, CredicarroResilienceOptions options) =>
        IsDocumentUpload(context) ? options.DocumentUploadAttemptTimeout : options.AttemptTimeout;

    /// <summary>
    /// Orçamento total da chamada: <c>TotalTimeout</c>; no envio de documento, <c>DocumentUploadAttemptTimeout × MaxRetryAttempts</c>.
    /// </summary>
    private static TimeSpan GetTotalTimeout(ResilienceContext context, CredicarroResilienceOptions options) =>
        IsDocumentUpload(context) ? UploadBudget(options) : options.TotalTimeout;

    private static TimeSpan UploadBudget(CredicarroResilienceOptions options) =>
        TimeSpan.FromTicks(options.DocumentUploadAttemptTimeout.Ticks * Math.Max(1, options.MaxRetryAttempts));

    private static TimeSpan MaxBudget(CredicarroResilienceOptions options)
    {
        var upload = UploadBudget(options);
        return upload > options.TotalTimeout ? upload : options.TotalTimeout;
    }

    /// <summary>Quanto resta do orçamento total da chamada lógica (nunca negativo).</summary>
    private static TimeSpan Remaining(ResilienceContext context, CredicarroResilienceOptions options)
    {
        var budget = GetTotalTimeout(context, options);

        if (!context.Properties.TryGetValue(ResilienceContextRequestKey.StartedAt, out var startedAt))
            return budget;

        var elapsed = TimeSpan.FromSeconds((double)(Stopwatch.GetTimestamp() - startedAt) / Stopwatch.Frequency);
        var remaining = budget - elapsed;

        return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
    }

    private static bool ShouldRetry(RetryPredicateArguments<HttpResponseMessage> args, CredicarroResilienceOptions options)
    {
        if (!args.Context.Properties.TryGetValue(ResilienceContextRequestKey.Key, out var request) || request is null)
            return false;

        if (!IdempotencyClassifier.IsRetrySafe(request))
            return false;

        if (!IsTransientFailure(args.Outcome, options))
            return false;

        // Retry-After maior que o orçamento restante: não espera, devolve a resposta (vira RateLimit com RetryAfter).
        return RetryAfterHeader.From(args.Outcome.Result) is not { } retryAfter || retryAfter <= Remaining(args.Context, options);
    }

    private static bool IsTransientFailure(Outcome<HttpResponseMessage> outcome, CredicarroResilienceOptions options)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
            return true;

        if (outcome.Result is null)
            return false;

        var statusCode = (int)outcome.Result.StatusCode;

        if (statusCode is 408 or 429 or 502 or 503 or 504)
            return true;

        return statusCode == 500 && options.RetryOn500;
    }

    /// <summary>
    /// Espera pelo <c>Retry-After</c> válido ou pelo backoff exponencial com jitter, sempre limitada ao orçamento restante.
    /// </summary>
    private static TimeSpan? GetRetryDelay(RetryDelayGeneratorArguments<HttpResponseMessage> args, CredicarroResilienceOptions options)
    {
        var delay = RetryAfterHeader.From(args.Outcome.Result) ?? Backoff(options.RetryBaseDelay, args.AttemptNumber);
        var remaining = Remaining(args.Context, options);

        return delay > remaining ? remaining : delay;
    }

    private static TimeSpan Backoff(TimeSpan baseDelay, int attempt)
    {
        _random ??= new Random(Guid.NewGuid().GetHashCode());

        var factor = Math.Pow(2, Math.Min(Math.Max(attempt, 0), 10));
        var jitter = 0.5 + (_random.NextDouble() * 0.5);

        return TimeSpan.FromTicks((long)(baseDelay.Ticks * factor * jitter));
    }
}
