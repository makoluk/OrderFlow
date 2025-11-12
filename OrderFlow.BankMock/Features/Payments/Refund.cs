using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.BankMock.Features.Payments;

public record RefundRequest(string PaymentId, decimal Amount, string Currency);
public record RefundResponse(string RefundId, string PaymentId, decimal Amount, string Currency);

public record RefundCommand(RefundRequest Request, string? Mode) : IRequest<BaseResponse<RefundResponse>>;

public class RefundCommandHandler : IRequestHandler<RefundCommand, BaseResponse<RefundResponse>>
{
	public async Task<BaseResponse<RefundResponse>> Handle(RefundCommand request, CancellationToken cancellationToken)
	{
		var mode = request.Mode?.ToLowerInvariant();

		if (mode == "fail")
		{
			await Task.Delay(100, cancellationToken);
			return BaseResponse<RefundResponse>.Fail("Bank service unavailable");
		}

		if (mode == "timeout")
		{
			await Task.Delay(5000, cancellationToken);
			var timeoutResp = new RefundResponse(Guid.NewGuid().ToString("N"), request.Request.PaymentId, request.Request.Amount, request.Request.Currency);
			return BaseResponse<RefundResponse>.Ok(timeoutResp);
		}

		await Task.Delay(Random.Shared.Next(100, 500), cancellationToken);

		if (Random.Shared.NextDouble() < 0.10)
			return BaseResponse<RefundResponse>.Fail("Bank returned 503");

		var resp = new RefundResponse(Guid.NewGuid().ToString("N"), request.Request.PaymentId, request.Request.Amount, request.Request.Currency);
		return BaseResponse<RefundResponse>.Ok(resp);
	}
}


