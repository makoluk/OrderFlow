using MediatR;
using OrderFlow.OrderService.Features.Basket;
using OrderFlow.Shared.Http;
using OrderFlow.OrderService.Services;

namespace OrderFlow.OrderService.Features.Orders;

public record CreateOrderFromBasketCommand(string CustomerId, string IdempotencyKey) : IRequest<IResult>;

public class CreateOrderFromBasketHandler : IRequestHandler<CreateOrderFromBasketCommand, IResult>
{
	private readonly IBasketStore _basketStore;
    private readonly IOrderCreationService _orderCreationService;
	private readonly ILogger<CreateOrderFromBasketHandler> _logger;

    public CreateOrderFromBasketHandler(IBasketStore basketStore, IOrderCreationService orderCreationService, ILogger<CreateOrderFromBasketHandler> logger)
	{
		_basketStore = basketStore;
        _orderCreationService = orderCreationService;
		_logger = logger;
	}

	public async Task<IResult> Handle(CreateOrderFromBasketCommand request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
		{
			return Results.BadRequest(BaseResponse<string>.Fail("Idempotency-Key header cannot be empty"));
		}

        // Idempotency shortcut BEFORE reading basket
        var (exists, existingOrderId) = await _orderCreationService.TryGetExistingOrderAsync(request.IdempotencyKey, cancellationToken);
        if (exists)
        {
            _logger.LogInformation("Idempotency-Key {Key} already processed (early). Returning existing OrderId={OrderId}", request.IdempotencyKey, existingOrderId);
            return Results.Ok(BaseResponse<CreateOrderResponse>.Ok(new CreateOrderResponse(existingOrderId), "Order already exists"));
        }

		var basket = _basketStore.Get(request.CustomerId);
		if (basket.Items.Count == 0)
			return Results.BadRequest(BaseResponse<string>.Fail("Basket is empty"));

		// currency validation
		var currencySet = basket.Items.Select(i => i.Currency).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		if (currencySet.Count > 1)
			return Results.BadRequest(BaseResponse<string>.Fail("Multiple currencies in basket are not supported"));

		var currency = currencySet.FirstOrDefault() ?? "TRY";
		var amount = basket.Total;
		if (amount <= 0)
			return Results.BadRequest(BaseResponse<string>.Fail("Basket total must be positive"));

        try
        {
            var orderId = await _orderCreationService.CreateOrderAsync(amount, currency, request.CustomerId, request.IdempotencyKey, cancellationToken);
            // Clear basket after successful creation & publish
            _basketStore.Clear(request.CustomerId);
            _logger.LogInformation("OrderCreated published from basket. OrderId={OrderId} Amount={Amount} {Currency}", orderId, amount, currency);
            return Results.Created($"/orders/{orderId}", BaseResponse<CreateOrderResponse>.Ok(new CreateOrderResponse(orderId), "Created"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order from basket with Idempotency-Key {Key}", request.IdempotencyKey);
            var error = BaseResponse<string>.Fail("An error occurred while creating the order from basket", ex.Message);
            return Results.Json(error, statusCode: 500);
        }
	}
}


