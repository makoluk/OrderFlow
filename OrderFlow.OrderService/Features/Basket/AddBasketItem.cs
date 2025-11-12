using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Features.Basket;

public record AddBasketItemCommand(string CustomerId, AddBasketItemRequest Request) : IRequest<BaseResponse<BasketDetailsResponse>>;

public class AddBasketItemHandler : IRequestHandler<AddBasketItemCommand, BaseResponse<BasketDetailsResponse>>
{
	private readonly IBasketStore _store;

	public AddBasketItemHandler(IBasketStore store)
	{
		_store = store;
	}

	public Task<BaseResponse<BasketDetailsResponse>> Handle(AddBasketItemCommand request, CancellationToken cancellationToken)
	{
		if (request.Request.Quantity <= 0 || request.Request.UnitPrice < 0)
			return Task.FromResult(BaseResponse<BasketDetailsResponse>.Fail("Invalid item values"));

		var snapshot = _store.AddItem(request.CustomerId, new BasketItem(
			request.Request.ProductId,
			request.Request.Name,
			request.Request.Quantity,
			request.Request.UnitPrice,
			request.Request.Currency));

		var response = new BasketDetailsResponse(snapshot.CustomerId, snapshot.Items, snapshot.Total);
		return Task.FromResult(BaseResponse<BasketDetailsResponse>.Ok(response));
	}
}


