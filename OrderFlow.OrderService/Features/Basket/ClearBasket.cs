using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Features.Basket;

public record ClearBasketCommand(string CustomerId) : IRequest<BaseResponse<string>>;

public class ClearBasketHandler : IRequestHandler<ClearBasketCommand, BaseResponse<string>>
{
	private readonly IBasketStore _store;

	public ClearBasketHandler(IBasketStore store)
	{
		_store = store;
	}

	public Task<BaseResponse<string>> Handle(ClearBasketCommand request, CancellationToken cancellationToken)
	{
		_store.Clear(request.CustomerId);
		return Task.FromResult(BaseResponse<string>.Ok("Cleared"));
	}
}


