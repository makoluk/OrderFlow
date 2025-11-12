namespace OrderFlow.OrderService.Features.Basket;

public record AddBasketItemRequest(string ProductId, string Name, int Quantity, decimal UnitPrice, string Currency);
public record BasketDetailsResponse(string CustomerId, IReadOnlyList<BasketItem> Items, decimal Total);


