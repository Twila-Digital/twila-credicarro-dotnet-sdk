using System.Diagnostics;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Credicarro.Errors;
using Credicarro.Http;

namespace Credicarro.Resilience;

internal sealed class ResilienceDelegatingHandler(ResiliencePipeline<HttpResponseMessage> pipeline) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await request.EnsureBufferedAsync().ConfigureAwait(false);

        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        context.Properties.Set(ResilienceContextRequestKey.Key, request);
        context.Properties.Set(ResilienceContextRequestKey.StartedAt, Stopwatch.GetTimestamp());

        Task<HttpResponseMessage>? executeTask = null;

        try
        {
            executeTask = pipeline.ExecuteAsync(
                static async (ctx, state) =>
                {
                    var attempt = state.Request.Clone();
                    return await state.Handler.SendAsyncCore(attempt, ctx.CancellationToken).ConfigureAwait(false);
                },
                context,
                (Handler: this, Request: request)).AsTask();

            return await WaitAsync(executeTask, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutRejectedException ex)
        {
            throw new CredicarroTimeoutException("A requisição excedeu o tempo limite configurado.", ex);
        }
        catch (BrokenCircuitException ex)
        {
            throw new CredicarroTimeoutException("O circuit breaker está aberto: chamadas recentes falharam de forma consistente.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new CredicarroTimeoutException("Falha de rede ou timeout ao chamar a API do Credicarro.", Sanitize(ex));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CredicarroTimeoutException("Falha de rede ou timeout ao chamar a API do Credicarro.", Sanitize(ex));
        }
        finally
        {
            ReturnWhenFinished(context, executeTask);
        }
    }

    /// <summary>
    /// Devolve o contexto ao pool só quando a execução termina. Se o chamador cancelou e a tarefa ainda está em voo,
    /// ela continua lendo as <c>Properties</c> do contexto; devolvê-lo antes permitiria que outra chamada o reutilizasse.
    /// </summary>
    private static void ReturnWhenFinished(ResilienceContext context, Task<HttpResponseMessage>? executeTask)
    {
        if (executeTask is null || executeTask.IsCompleted)
        {
            ResilienceContextPool.Shared.Return(context);
            return;
        }

        _ = executeTask.ContinueWith(
            static (task, state) =>
            {
                _ = task.Exception;
                ResilienceContextPool.Shared.Return((ResilienceContext)state!);
            },
            context,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    /// <summary>
    /// Causa sem o objeto de requisição/resposta da biblioteca, sem cabeçalhos e sem corpo: só os tipos das exceções.
    /// </summary>
    private static Exception Sanitize(Exception exception)
    {
        var inner = exception.InnerException;

        return new Exception(inner is null
            ? exception.GetType().Name
            : exception.GetType().Name + " -> " + inner.GetType().Name);
    }

    private Task<HttpResponseMessage> SendAsyncCore(HttpRequestMessage request, CancellationToken cancellationToken) =>
        base.SendAsync(request, cancellationToken);

    private static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
    {
        if (task.IsCompleted || !cancellationToken.CanBeCanceled)
            return await task.ConfigureAwait(false);

        var cancellationTcs = new TaskCompletionSource<object?>();

        using (cancellationToken.Register(static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null), cancellationTcs))
        {
            var completed = await Task.WhenAny(task, cancellationTcs.Task).ConfigureAwait(false);

            if (completed == cancellationTcs.Task)
                cancellationToken.ThrowIfCancellationRequested();
        }

        return await task.ConfigureAwait(false);
    }
}
