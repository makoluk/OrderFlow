using System.Diagnostics;
using MassTransit;
using OrderFlow.PaymentService.Data;
using OrderFlow.PaymentService.Entities;
using OrderFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrderFlow.PaymentService.Consumers;

public class RefundRequestedConsumer : IConsumer<RefundRequested>
{
    private readonly ILogger<RefundRequestedConsumer> _logger;
    private readonly IHttpClientFactory _http;
    private readonly PaymentDbContext _dbContext;

    public RefundRequestedConsumer(ILogger<RefundRequestedConsumer> logger, IHttpClientFactory http, PaymentDbContext dbContext)
    {
        _logger = logger;
        _http = http;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<RefundRequested> ctx)
    {
        var messageId = ctx.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var messageType = typeof(RefundRequested).FullName ?? nameof(RefundRequested);
        var correlationId = Activity.Current?.TraceId.ToString();

        // Idempotency kontrolü: Bu mesaj daha önce işlendi mi?
        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AnyAsync(p => p.MessageId == messageId);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Refund message already processed, skipping | MessageId={MessageId} OrderId={OrderId}",
                messageId, ctx.Message.OrderId);
            return;
        }

        var m = ctx.Message;
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        _logger.LogInformation(
            "RefundRequested consumed | OrderId={OrderId} Amount={Amount}{Currency} Reason={Reason} MessageId={MessageId}",
            m.OrderId, m.Amount, m.Currency, m.Reason, messageId);

        // DB transaction içinde işlemleri yap
        // UseInMemoryOutbox sayesinde event'ler consumer başarılı olduğunda publish edilecek
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // Payment kaydını bul
            var payment = await _dbContext.Payments
                .FirstOrDefaultAsync(p => p.OrderId == m.OrderId);

            if (payment == null)
            {
                _logger.LogWarning("Payment not found for refund | OrderId={OrderId}", m.OrderId);
                // Payment bulunamadı ama mesajı işlenmiş olarak işaretle (duplicate'ı önlemek için)
                _dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = messageType,
                    ProcessedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }

            // BankMock refund çağrısı (Polly policies: Timeout 3s, Retry 3x exponential, CircuitBreaker 5 failures → 30s)
            var client = _http.CreateClient("bank");
            HttpResponseMessage? response = null;
            
            try
            {
                response = await client.PostAsJsonAsync("/refund", new 
                { 
                    PaymentId = payment.PaymentId ?? payment.Id.ToString(),
                    Amount = m.Amount, 
                    Currency = m.Currency 
                });
            }
            catch (BrokenCircuitException ex)
            {
                // Circuit breaker açık - servis kullanılamıyor
                _logger.LogError(ex, "Circuit breaker is OPEN - BankMock service unavailable for refund | OrderId={OrderId}", m.OrderId);
                // Refund başarısız ama mesajı işlenmiş olarak işaretle
                _dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = messageType,
                    ProcessedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }
            catch (TimeoutRejectedException ex)
            {
                // Timeout - 3 saniye içinde yanıt alınamadı
                _logger.LogWarning(ex, "Refund request timeout after 3s | OrderId={OrderId}", m.OrderId);
                // Refund başarısız ama mesajı işlenmiş olarak işaretle
                _dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = messageType,
                    ProcessedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                var reason = response != null 
                    ? $"refund_bank_status={(int)response.StatusCode}" 
                    : "refund_unknown_error";
                _logger.LogWarning("Refund failed | OrderId={OrderId} Reason={Reason}", m.OrderId, reason);
                
                // Refund başarısız ama mesajı işlenmiş olarak işaretle
                _dbContext.ProcessedMessages.Add(new ProcessedMessage
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    MessageType = messageType,
                    ProcessedAt = DateTime.UtcNow,
                    CorrelationId = correlationId
                });
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                return;
            }

            // Refund başarılı
            var refundResult = await response.Content.ReadFromJsonAsync<RefundResponse>();
            var refundId = refundResult?.RefundId ?? Guid.NewGuid().ToString("N");

            // PaymentRefunded event'i publish et
            await ctx.Publish(new PaymentRefunded(
                m.OrderId,
                payment.PaymentId ?? payment.Id.ToString(),
                m.Amount,
                m.Currency,
                refundId,
                DateTime.UtcNow));

            // Payment durumunu güncelle
            payment.Status = "Refunded";
            payment.UpdatedAt = DateTime.UtcNow;

            // İşlenen mesajı kaydet
            _dbContext.ProcessedMessages.Add(new ProcessedMessage
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                MessageType = messageType,
                ProcessedAt = DateTime.UtcNow,
                CorrelationId = correlationId
            });
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("PaymentRefunded published | OrderId={OrderId} PaymentId={PaymentId} RefundId={RefundId}", 
                m.OrderId, payment.PaymentId, refundId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing RefundRequested | OrderId={OrderId} MessageId={MessageId}", m.OrderId, messageId);
            throw;
        }
    }

    private record RefundResponse(string RefundId, string PaymentId, decimal Amount, string Currency);
}

