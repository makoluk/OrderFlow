using System.Diagnostics;
using MassTransit;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.StockService.Consumers;

public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
{
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    public PaymentSucceededConsumer(ILogger<PaymentSucceededConsumer> logger) => _logger = logger;

    public async Task Consume(ConsumeContext<PaymentSucceeded> ctx)
    {
        var m = ctx.Message;
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        try
        {
            _logger.LogInformation("Stock reserve started | OrderId={OrderId}", m.OrderId);

            // DEMO: gerçek stok düşme
            await Task.Delay(150);

            // TEST: Belirli orderId'lerde hata fırlat
            // OrderId'nin son karakteri "f" veya "9" ise fail yap (test için)
            var orderIdStr = m.OrderId.ToString();
            if (orderIdStr.EndsWith("f") || orderIdStr.EndsWith("9"))
            {
                throw new Exception("Simulated stock reserve failure");
            }

            await ctx.Publish(new StockReserved(m.OrderId, DateTime.UtcNow));
            _logger.LogInformation("StockReserved published | OrderId={OrderId}", m.OrderId);
        }
        catch (Exception ex)
        {
            await ctx.Publish(new StockReserveFailed(m.OrderId, ex.Message, DateTime.UtcNow));
            _logger.LogError(ex, "StockReserveFailed | OrderId={OrderId}", m.OrderId);
        }
    }
}

