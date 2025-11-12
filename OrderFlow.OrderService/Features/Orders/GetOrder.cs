using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderFlow.OrderService.Data;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Features.Orders;

public record GetOrderQuery(Guid OrderId) : IRequest<IResult>;

public record OrderDetailsResponse(
    Guid Id,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? PaidAtUtc,
    DateTime? StockReservedAtUtc,
    DateTime? EmailSentAtUtc,
    DateTime? CompletedAtUtc,
    string? FailReason,
    DateTime? FailedAtUtc,
    string? CorrelationId,
    DateTime UpdatedAtUtc
);

public class GetOrderHandler : IRequestHandler<GetOrderQuery, IResult>
{
    private readonly OrderDbContext _dbContext;

    public GetOrderHandler(OrderDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IResult> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Payment)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

        if (order == null)
            return Results.NotFound(BaseResponse<string>.Fail("Order not found"));

        // Derive a stable status from timestamps to avoid out-of-order event overrides
        string derivedStatus =
            order.CompletedAtUtc.HasValue ? "Completed" :
            order.FailedAtUtc.HasValue ? "Failed" :
            order.EmailSentAtUtc.HasValue ? "EmailSent" :
            order.StockReservedAtUtc.HasValue ? "StockReserved" :
            order.PaidAtUtc.HasValue ? "Paid" :
            "Created";

        var dto = new OrderDetailsResponse(
            order.Id,
            derivedStatus,
            order.CreatedAt,
            order.PaidAtUtc,
            order.StockReservedAtUtc,
            order.EmailSentAtUtc,
            order.CompletedAtUtc,
            order.FailReason,
            order.FailedAtUtc,
            order.CorrelationId,
            order.UpdatedAt
        );

        return Results.Ok(BaseResponse<OrderDetailsResponse>.Ok(dto));
    }
}


