using Polly;

namespace Credicarro.Resilience;

internal static class ResilienceContextRequestKey
{
    public static readonly ResiliencePropertyKey<HttpRequestMessage> Key = new("Credicarro.HttpRequestMessage");

    /// <summary>Instante (<see cref="System.Diagnostics.Stopwatch.GetTimestamp"/>) em que a chamada lógica começou.</summary>
    public static readonly ResiliencePropertyKey<long> StartedAt = new("Credicarro.StartedAt");
}
