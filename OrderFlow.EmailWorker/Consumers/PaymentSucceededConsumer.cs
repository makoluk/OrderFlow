using System.Diagnostics;
using MassTransit;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.EmailWorker.Consumers;

public class PaymentSucceededConsumer : IConsumer<PaymentSucceeded>
{
    private readonly ILogger<PaymentSucceededConsumer> _logger;
    public PaymentSucceededConsumer(ILogger<PaymentSucceededConsumer> logger) => _logger = logger;

    public async Task Consume(ConsumeContext<PaymentSucceeded> ctx)
    {
        var m = ctx.Message;
        var retryAttempt = ctx.GetRetryAttempt();
        
        _logger.LogInformation("Consume TraceId={TraceId} RetryAttempt={RetryAttempt}", 
            Activity.Current?.TraceId, retryAttempt);
        
        try
        {
            _logger.LogInformation("Sending receipt email | OrderId={OrderId} RetryAttempt={RetryAttempt}", 
                m.OrderId, retryAttempt);
            
            // Email gönderme simülasyonu
            await Task.Delay(120);

            // TEST: Belirli orderId'lerde hata fırlat
            // OrderId'nin son karakteri "2" veya "b" ise fail yap (test için)
            // VEYA Amount 250'den büyükse fail (test için)
            var orderIdStr = m.OrderId.ToString();
            if (orderIdStr.EndsWith("2") || orderIdStr.EndsWith("b") || m.Amount > 250)
            {
                throw new Exception($"Simulated email sending failure (attempt {retryAttempt + 1})");
            }

            await ctx.Publish(new ReceiptEmailSent(m.OrderId, "customer@example.com", DateTime.UtcNow));
            _logger.LogInformation("ReceiptEmailSent published | OrderId={OrderId} RetryAttempt={RetryAttempt}", 
                m.OrderId, retryAttempt);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Email sending failed | OrderId={OrderId} RetryAttempt={RetryAttempt} Message={Message}", 
                m.OrderId, retryAttempt, ex.Message);
            
            // Retry limit'e ulaşıldıysa (3 retry = 4 toplam deneme) ReceiptEmailFailed publish et
            // retryAttempt: 0=ilk deneme, 1=1. retry, 2=2. retry (retryLimit=3 olduğu için max retryAttempt=2)
            if (retryAttempt >= 2)
            {
                // Son denemede başarısız oldu, ReceiptEmailFailed publish et
                await ctx.Publish(new ReceiptEmailFailed(m.OrderId, 
                    $"Email sending failed after {retryAttempt + 1} attempts: {ex.Message}", 
                    DateTime.UtcNow));
                _logger.LogError(ex, 
                    "ReceiptEmailFailed published after max retries | OrderId={OrderId} RetryAttempt={RetryAttempt}", 
                    m.OrderId, retryAttempt);
                return; // Exception fırlatma, mesaj başarıyla işlendi (ama email gönderilemedi)
            }
            
            // Henüz retry limit'e ulaşılmadı, exception'ı tekrar fırlat ki MassTransit retry yapsın
            throw;
        }
    }
}

