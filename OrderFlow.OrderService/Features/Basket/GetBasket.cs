using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Features.Basket;

public record GetBasketQuery(string CustomerId) : IRequest<BaseResponse<BasketDetailsResponse>>;

public class GetBasketHandler : IRequestHandler<GetBasketQuery, BaseResponse<BasketDetailsResponse>>
{
	private readonly IBasketStore _store;

	public GetBasketHandler(IBasketStore store)
	{
		_store = store;
	}

	public Task<BaseResponse<BasketDetailsResponse>> Handle(GetBasketQuery request, CancellationToken cancellationToken)
	{
		var snapshot = _store.Get(request.CustomerId);
		var response = new BasketDetailsResponse(snapshot.CustomerId, snapshot.Items, snapshot.Total);
		return Task.FromResult(BaseResponse<BasketDetailsResponse>.Ok(response));
	}
}


