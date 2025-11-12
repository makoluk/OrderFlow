using MediatR;
using OrderFlow.Shared.Http;
using OrderFlow.OrderService.Services;

namespace OrderFlow.OrderService.Features.Orders;

public record CreateOrderResponse(Guid OrderId);
public record CreateOrderRequest(decimal Amount, string? Currency, string? CustomerId);

public record CreateOrderCommand(decimal Amount, string? Currency, string? CustomerId, string IdempotencyKey) : IRequest<IResult>;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, IResult>
{
    private readonly IOrderCreationService _orderCreationService;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(IOrderCreationService orderCreationService, ILogger<CreateOrderHandler> logger)
    {
        _orderCreationService = orderCreationService;
        _logger = logger;
    }

    public async Task<IResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            return Results.BadRequest(BaseResponse<string>.Fail("Idempotency-Key header cannot be empty"));
        }

        var (exists, existingOrderId) = await _orderCreationService.TryGetExistingOrderAsync(request.IdempotencyKey, cancellationToken);
        if (exists)
        {
            _logger.LogInformation("Idempotency-Key {Key} already processed. Returning existing OrderId={OrderId}", request.IdempotencyKey, existingOrderId);
            return Results.Ok(BaseResponse<CreateOrderResponse>.Ok(new CreateOrderResponse(existingOrderId), "Order already exists"));
        }

        var currency = request.Currency ?? "TRY";
        try
        {
            var orderId = await _orderCreationService.CreateOrderAsync(request.Amount, currency, request.CustomerId, request.IdempotencyKey, cancellationToken);
            return Results.Created($"/orders/{orderId}", BaseResponse<CreateOrderResponse>.Ok(new CreateOrderResponse(orderId), "Created"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order with Idempotency-Key {Key}", request.IdempotencyKey);
            var error = BaseResponse<string>.Fail("An error occurred while creating the order", ex.Message);
            return Results.Json(error, statusCode: 500);
        }
    }
}


