using System.Net;
using Polly;

namespace OrderFlow.PaymentService.Policies;

public static class HttpClientPolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger = null) =>
        Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.InternalServerError || // 500
                r.StatusCode == HttpStatusCode.ServiceUnavailable ||   // 503
                r.StatusCode == HttpStatusCode.RequestTimeout ||       // 408
                r.StatusCode == HttpStatusCode.TooManyRequests ||      // 429
                r.StatusCode == HttpStatusCode.BadGateway ||           // 502
                r.StatusCode == HttpStatusCode.GatewayTimeout)         // 504
            .WaitAndRetryAsync(
                retryCount: 3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger?.LogWarning(
                        "Retry attempt {RetryCount} after {Delay}s | StatusCode={StatusCode}",
                        retryCount, timespan.TotalSeconds, outcome.Result?.StatusCode);
                }
            );

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger? logger = null) =>
        Policy
            .HandleResult<HttpResponseMessage>(r =>
                r.StatusCode == HttpStatusCode.InternalServerError || // 500
                r.StatusCode == HttpStatusCode.ServiceUnavailable)    // 503
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, timespan) =>
                {
                    logger?.LogError(
                        "Circuit breaker OPEN for {Duration}s | StatusCode={StatusCode}",
                        timespan.TotalSeconds, result.Result?.StatusCode);
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit breaker CLOSED - service recovered");
                },
                onHalfOpen: () =>
                {
                    logger?.LogInformation("Circuit breaker HALF-OPEN - testing service recovery");
                }
            );

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy() =>
        Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(3));
}


