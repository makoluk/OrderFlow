using MassTransit;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace OrderFlow.Shared.Contracts.Tracing;

public class TraceContextConsumeObserver : IConsumeObserver
{
    public Task PreConsume<T>(ConsumeContext<T> context) where T : class
    {
        var propagator = Propagators.DefaultTextMapPropagator;

        // Extract parent context from headers
        var parentContext = propagator.Extract(default, context, static (ctx, key) =>
        {
            return ctx.Headers.TryGetHeader(key, out var value)
                ? new[] { value?.ToString() ?? string.Empty }
                : Array.Empty<string>();
        });

        // Set parent context for MassTransit instrumentation to use
        if (parentContext.ActivityContext != default)
        {
            // Create a temporary Activity with parent context - MassTransit instrumentation will use this
            var activity = new Activity("MassTransit.Consume")
                .SetParentId(parentContext.ActivityContext.TraceId, parentContext.ActivityContext.SpanId, parentContext.ActivityContext.TraceFlags);
            activity.Start();
            Activity.Current = activity;
        }

        Baggage.Current = parentContext.Baggage;
        return Task.CompletedTask;
    }

    public Task PostConsume<T>(ConsumeContext<T> context) where T : class
    {
        // Stop the temporary Activity - MassTransit instrumentation creates its own spans
        Activity.Current?.Stop();
        return Task.CompletedTask;
    }

    public Task ConsumeFault<T>(ConsumeContext<T> context, Exception exception) where T : class
    {
        Activity.Current?.Stop();
        return Task.CompletedTask;
    }
}

