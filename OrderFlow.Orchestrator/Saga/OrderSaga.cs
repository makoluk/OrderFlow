using MassTransit;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.Orchestrator.Saga;

public class OrderSaga : MassTransitStateMachine<OrderState>
{
    // States - StateMachine sınıfından türetilen State tipi
    // Namespace çakışmasını önlemek için MassTransit.State kullanıyoruz
    public MassTransit.State WaitingPayment { get; private set; } = null!;
    public MassTransit.State Paid { get; private set; } = null!;
    public MassTransit.State StockOk { get; private set; } = null!;
    public MassTransit.State Completed { get; private set; } = null!;
    public MassTransit.State Failed { get; private set; } = null!;

    // Events
    public Event<OrderCreated> OrderCreated { get; private set; } = null!;
    public Event<PaymentSucceeded> PaymentSucceeded { get; private set; } = null!;
    public Event<PaymentFailed> PaymentFailed { get; private set; } = null!;
    public Event<StockReserved> StockReserved { get; private set; } = null!;
    public Event<StockReserveFailed> StockReserveFailed { get; private set; } = null!;
    public Event<ReceiptEmailSent> ReceiptEmailSent { get; private set; } = null!;
    public Event<ReceiptEmailFailed> ReceiptEmailFailed { get; private set; } = null!;
    public Event<PaymentRefunded> PaymentRefunded { get; private set; } = null!;

    // Schedule (Timeout)
    public Schedule<OrderState, PaymentTimeoutExpired> PaymentTimeout { get; private set; } = null!;

    public OrderSaga()
    {
        SetCompletedWhenFinalized();
        InstanceState(x => x.CurrentState);

        // Event correlation
        Event(() => OrderCreated, x => x.CorrelateById(m => m.Message.OrderId));
        Event(() => PaymentSucceeded, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                Amount = ctx.Message.Amount,
                Currency = ctx.Message.Currency,
                PaidAtUtc = ctx.Message.SucceededAtUtc
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => PaymentFailed, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                FailedAtUtc = ctx.Message.FailedAtUtc,
                FailReason = ctx.Message.Reason
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => StockReserved, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                StockReservedAtUtc = ctx.Message.ReservedAtUtc
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => StockReserveFailed, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                FailedAtUtc = ctx.Message.FailedAtUtc,
                FailReason = ctx.Message.Reason
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => ReceiptEmailSent, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                EmailSentAtUtc = ctx.Message.SentAtUtc
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => ReceiptEmailFailed, x =>
        {
            x.CorrelateById(m => m.Message.OrderId);
            x.InsertOnInitial = true;
            x.SetSagaFactory(ctx => new OrderState
            {
                CorrelationId = ctx.Message.OrderId,
                OrderId = ctx.Message.OrderId,
                FailedAtUtc = ctx.Message.FailedAtUtc,
                FailReason = ctx.Message.Reason
            });
            x.OnMissingInstance(m => m.Discard());
        });
        Event(() => PaymentRefunded, x => x.CorrelateById(m => m.Message.OrderId));

        // Schedule configuration
        Schedule(() => PaymentTimeout, x => x.PaymentTimeoutTokenId, s =>
        {
            s.Delay = TimeSpan.FromSeconds(30); // 30 saniye timeout
            s.Received = r => r.CorrelateById(m => m.Message.OrderId);
        });

        // Initially: OrderCreated event'i geldiğinde
        Initially(
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CorrelationId = ctx.Message.OrderId; // Saga correlation ID = OrderId
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                })
                .TransitionTo(WaitingPayment)
                .Schedule(PaymentTimeout, ctx => new PaymentTimeoutExpired(ctx.Saga.OrderId))
        ,
            // Eğer OrderCreated kaçırıldıysa, PaymentSucceeded ile başlat
            When(PaymentSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CorrelationId = ctx.Message.OrderId;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                    ctx.Saga.PaidAtUtc = ctx.Message.SucceededAtUtc;
                })
                .TransitionTo(Paid),

            // StockReserved ilk gelen olursa
            When(StockReserved)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CorrelationId = ctx.Message.OrderId;
                    ctx.Saga.StockReservedAtUtc = ctx.Message.ReservedAtUtc;
                })
                .TransitionTo(StockOk),

            // ReceiptEmailSent ilk gelen olursa
            When(ReceiptEmailSent)
                .Then(ctx =>
                {
                    ctx.Saga.OrderId = ctx.Message.OrderId;
                    ctx.Saga.CorrelationId = ctx.Message.OrderId;
                    ctx.Saga.EmailSentAtUtc = ctx.Message.SentAtUtc;
                })
                .IfElse(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value))
                                .TransitionTo(Completed).Finalize(),
                    elseB => elseB.TransitionTo(WaitingPayment)
                                  .Schedule(PaymentTimeout, c => new PaymentTimeoutExpired(c.Saga.OrderId)))
        );

        // WaitingPayment state'inde
        During(WaitingPayment,
            // Payment başarılı oldu
            When(PaymentSucceeded)
                .Then(ctx =>
                {
                    ctx.Saga.PaidAtUtc = ctx.Message.SucceededAtUtc;
                    ctx.Saga.Amount = ctx.Message.Amount;
                    ctx.Saga.Currency = ctx.Message.Currency;
                })
                .Unschedule(PaymentTimeout) // Timeout'u iptal et
                .TransitionTo(Paid),

            // Payment başarısız oldu
            When(PaymentFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc;
                    ctx.Saga.FailReason = ctx.Message.Reason;
                })
                .Unschedule(PaymentTimeout) // Timeout'u iptal et
                .Publish(ctx => new OrderFailed(ctx.Saga.OrderId, ctx.Saga.FailReason ?? "Payment failed", DateTime.UtcNow))
                .TransitionTo(Failed)
                .Finalize(),

            // Payment timeout - refund iste
            When(PaymentTimeout.Received)
                .Then(ctx =>
                {
                    ctx.Saga.FailedAtUtc = DateTime.UtcNow;
                    ctx.Saga.FailReason = "Payment timeout - refund requested";
                })
                .IfElse(ctx => ctx.Saga.Amount.HasValue && !string.IsNullOrEmpty(ctx.Saga.Currency),
                    then => then
                        .Publish(ctx => new RefundRequested(
                            ctx.Saga.OrderId,
                            ctx.Saga.Amount!.Value,
                            ctx.Saga.Currency!,
                            "payment_timeout",
                            DateTime.UtcNow)),
                    elseB => elseB
                        .Publish(ctx => new OrderFailed(ctx.Saga.OrderId, "Payment timeout", DateTime.UtcNow)))
                .TransitionTo(Failed)
                .Finalize()
        );

        // Paid state'inde
        During(Paid,
            // Stock reserved oldu
            When(StockReserved)
                .Then(ctx =>
                {
                    ctx.Saga.StockReservedAtUtc = ctx.Message.ReservedAtUtc;
                })
                .IfElse(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value))
                                .TransitionTo(Completed).Finalize(),
                    elseB => elseB.TransitionTo(StockOk)),

            // Stock reserve başarısız - refund iste
            When(StockReserveFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc;
                    ctx.Saga.FailReason = ctx.Message.Reason;
                })
                .IfElse(ctx => ctx.Saga.Amount.HasValue && !string.IsNullOrEmpty(ctx.Saga.Currency),
                    then => then
                        .Publish(ctx => new RefundRequested(
                            ctx.Saga.OrderId,
                            ctx.Saga.Amount!.Value,
                            ctx.Saga.Currency!,
                            $"stock_failed: {ctx.Saga.FailReason}",
                            DateTime.UtcNow)),
                    elseB => elseB
                        .Publish(ctx => new OrderFailed(ctx.Saga.OrderId, ctx.Saga.FailReason ?? "Stock reserve failed", DateTime.UtcNow)))
                .TransitionTo(Failed)
                .Finalize()
        );

        // StockReserved state'inde
        During(StockOk,
            // Email gönderildi
            When(ReceiptEmailSent)
                .Then(ctx =>
                {
                    ctx.Saga.EmailSentAtUtc = ctx.Message.SentAtUtc;
                })
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    x => x.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                          .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value))
                          .TransitionTo(Completed)
                          .Finalize()),

            // Email gönderme başarısız - opsiyonel, sipariş tamamlanabilir
            When(ReceiptEmailFailed)
                .Then(ctx =>
                {
                    ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc;
                    ctx.Saga.FailReason = ctx.Message.Reason;
                })
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    x => x.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                          .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value))
                          .TransitionTo(Completed).Finalize())
        );

        // Out-of-order events: handle in any state; publish Completed once required steps are set
        DuringAny(
            // Geç gelen OrderCreated: eksik alanları tamamla
            When(OrderCreated)
                .Then(ctx =>
                {
                    ctx.Saga.Amount ??= ctx.Message.Amount;
                    ctx.Saga.Currency ??= ctx.Message.Currency;
                }),
            When(ReceiptEmailSent)
                .Then(ctx => { ctx.Saga.EmailSentAtUtc = ctx.Message.SentAtUtc; })
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                 .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value)).TransitionTo(Completed).Finalize()),

            When(StockReserved)
                .Then(ctx => { ctx.Saga.StockReservedAtUtc = ctx.Message.ReservedAtUtc; })
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                 .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value)).TransitionTo(Completed).Finalize()),

            When(PaymentSucceeded)
                .Then(ctx => { ctx.Saga.PaidAtUtc = ctx.Message.SucceededAtUtc; ctx.Saga.Amount = ctx.Message.Amount; ctx.Saga.Currency = ctx.Message.Currency; })
                .Unschedule(PaymentTimeout)
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                 .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value)).TransitionTo(Completed).Finalize()),

            // Handle failures arriving out of order as well
            When(ReceiptEmailFailed)
                .Then(ctx => { ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc; ctx.Saga.FailReason = ctx.Message.Reason; })
                .If(ctx => !ctx.Saga.CompletedAtUtc.HasValue && ctx.Saga.PaidAtUtc.HasValue && ctx.Saga.StockReservedAtUtc.HasValue,
                    then => then.Then(c => c.Saga.CompletedAtUtc = DateTime.UtcNow)
                                 .Publish(ctx => new OrderCompleted(ctx.Saga.OrderId, ctx.Saga.CompletedAtUtc!.Value)).TransitionTo(Completed).Finalize()),

            When(StockReserveFailed)
                .Then(ctx => { ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc; ctx.Saga.FailReason = ctx.Message.Reason; })
                .IfElse(ctx => ctx.Saga.Amount.HasValue && !string.IsNullOrEmpty(ctx.Saga.Currency),
                    then => then.Publish(ctx => new RefundRequested(ctx.Saga.OrderId, ctx.Saga.Amount!.Value, ctx.Saga.Currency!, $"stock_failed: {ctx.Saga.FailReason}", DateTime.UtcNow)).TransitionTo(Failed).Finalize(),
                    elseB => elseB.Publish(ctx => new OrderFailed(ctx.Saga.OrderId, ctx.Saga.FailReason ?? "Stock reserve failed", DateTime.UtcNow)).TransitionTo(Failed).Finalize())
            ,

            // Payment failed may arrive before OrderCreated (or out-of-order)
            When(PaymentFailed)
                .Then(ctx => { ctx.Saga.FailedAtUtc = ctx.Message.FailedAtUtc; ctx.Saga.FailReason = ctx.Message.Reason; })
                .IfElse(ctx => ctx.Saga.Amount.HasValue && !string.IsNullOrEmpty(ctx.Saga.Currency),
                    then => then.Publish(ctx => new RefundRequested(ctx.Saga.OrderId, ctx.Saga.Amount!.Value, ctx.Saga.Currency!, $"payment_failed: {ctx.Saga.FailReason}", DateTime.UtcNow)).TransitionTo(Failed).Finalize(),
                    elseB => elseB.Publish(ctx => new OrderFailed(ctx.Saga.OrderId, ctx.Saga.FailReason ?? "Payment failed", DateTime.UtcNow)).TransitionTo(Failed).Finalize())
        );


    }
}

