using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Features.Basket;

public record RemoveBasketItemCommand(string CustomerId, string ProductId) : IRequest<BaseResponse<BasketDetailsResponse>>;

public class RemoveBasketItemHandler : IRequestHandler<RemoveBasketItemCommand, BaseResponse<BasketDetailsResponse>>
{
	private readonly IBasketStore _store;

	public RemoveBasketItemHandler(IBasketStore store)
	{
		_store = store;
	}

	public Task<BaseResponse<BasketDetailsResponse>> Handle(RemoveBasketItemCommand request, CancellationToken cancellationToken)
	{
		var snapshot = _store.RemoveItem(request.CustomerId, request.ProductId);
		var response = new BasketDetailsResponse(snapshot.CustomerId, snapshot.Items, snapshot.Total);
		return Task.FromResult(BaseResponse<BasketDetailsResponse>.Ok(response));
	}
}


