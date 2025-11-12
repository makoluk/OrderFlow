using System.Diagnostics;
using MassTransit;
using OrderFlow.PaymentService.Data;
using OrderFlow.PaymentService.Entities;
using OrderFlow.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace OrderFlow.PaymentService.Consumers;

public class OrderCreatedConsumer : IConsumer<OrderCreated>
{
    private readonly ILogger<OrderCreatedConsumer> _logger;
    private readonly IHttpClientFactory _http;
    private readonly PaymentDbContext _dbContext;

    public OrderCreatedConsumer(ILogger<OrderCreatedConsumer> logger, IHttpClientFactory http, PaymentDbContext dbContext)
    {
        _logger = logger;
        _http = http;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<OrderCreated> ctx)
    {
        var messageId = ctx.MessageId?.ToString() ?? Guid.NewGuid().ToString();
        var messageType = typeof(OrderCreated).FullName ?? nameof(OrderCreated);
        var correlationId = Activity.Current?.TraceId.ToString();

        // Idempotency kontrolü: Bu mesaj daha önce işlendi mi?
        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AnyAsync(p => p.MessageId == messageId);

        if (alreadyProcessed)
        {
            _logger.LogInformation(
                "Message already processed, skipping | MessageId={MessageId} OrderId={OrderId}",
                messageId, ctx.Message.OrderId);
            return;
        }

        var m = ctx.Message;
        _logger.LogInformation("Consume TraceId={TraceId}", Activity.Current?.TraceId);
        _logger.LogInformation(
            "OrderCreated consumed | OrderId={OrderId} Amount={Amount}{Currency} MessageId={MessageId}",
            m.OrderId, m.Amount, m.Currency, messageId);

        // DB transaction içinde işlemleri yap
        // UseInMemoryOutbox sayesinde event'ler consumer başarılı olduğunda publish edilecek
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            // BankMock çağrısı (Polly policies: Timeout 3s, Retry 3x exponential, CircuitBreaker 5 failures → 30s)
            var client = _http.CreateClient("bank");
            HttpResponseMessage? response = null;
            
            try
            {
                response = await client.PostAsJsonAsync("/charge", new { Amount = m.Amount, Currency = m.Currency });
            }
            catch (BrokenCircuitException ex)
            {
                // Circuit breaker açık - servis kullanılamıyor
                _logger.LogError(ex, "Circuit breaker is OPEN - BankMock service unavailable | OrderId={OrderId}", m.OrderId);
                var reason = "circuit_breaker_open";
                await ctx.Publish(new PaymentFailed(m.OrderId, reason, DateTime.UtcNow));
                
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
                
                return;
            }
            catch (TimeoutRejectedException ex)
            {
                // Timeout - 3 saniye içinde yanıt alınamadı
                _logger.LogWarning(ex, "Request timeout after 3s | OrderId={OrderId}", m.OrderId);
                var reason = "timeout";
                await ctx.Publish(new PaymentFailed(m.OrderId, reason, DateTime.UtcNow));
                
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
                
                return;
            }

            if (response == null || !response.IsSuccessStatusCode)
            {
                var reason = response != null 
                    ? $"bank_status={(int)response.StatusCode}" 
                    : "unknown_error";
                await ctx.Publish(new PaymentFailed(m.OrderId, reason, DateTime.UtcNow));
                
                // İşlenen mesajı kaydet (başarısız olsa bile duplicate'ı önlemek için)
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
                
                _logger.LogWarning("PaymentFailed published | OrderId={OrderId} Reason={Reason}", m.OrderId, reason);
                return;
            }

            var paymentId = Guid.NewGuid().ToString("N"); // demo amaçlı
            await ctx.Publish(new PaymentSucceeded(m.OrderId, m.CustomerId, paymentId, m.Amount, m.Currency, DateTime.UtcNow));

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

            _logger.LogInformation("PaymentSucceeded published | OrderId={OrderId} PaymentId={PaymentId}", m.OrderId, paymentId);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error processing OrderCreated | OrderId={OrderId} MessageId={MessageId}", m.OrderId, messageId);
            throw;
        }
    }
}
