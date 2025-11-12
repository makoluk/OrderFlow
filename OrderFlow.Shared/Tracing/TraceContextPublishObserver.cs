using MassTransit;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using System.Diagnostics;

namespace OrderFlow.Shared.Contracts.Tracing;

public class TraceContextPublishObserver : IPublishObserver
{
    public Task PrePublish<T>(PublishContext<T> context) where T : class
    {
        var activity = Activity.Current;
        if (activity == null)
            return Task.CompletedTask;

        var propagator = Propagators.DefaultTextMapPropagator;
        propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), context,
            static (ctx, key, value) => ctx.Headers.Set(key, value));

        return Task.CompletedTask;
    }

    public Task PostPublish<T>(PublishContext<T> context) where T : class => Task.CompletedTask;
    public Task PublishFault<T>(PublishContext<T> context, Exception exception) where T : class => Task.CompletedTask;
}

