using MediatR;
using OrderFlow.Shared.Http;

namespace OrderFlow.BankMock.Features.Payments;

public record ChargeRequest(decimal Amount, string Currency);
public record ChargeResponse(string AuthCode, decimal Amount, string Currency);

public record ChargeCommand(ChargeRequest Request, string? Mode) : IRequest<BaseResponse<ChargeResponse>>;

public class ChargeCommandHandler : IRequestHandler<ChargeCommand, BaseResponse<ChargeResponse>>
{
	public async Task<BaseResponse<ChargeResponse>> Handle(ChargeCommand request, CancellationToken cancellationToken)
	{
		var mode = request.Mode?.ToLowerInvariant();

		if (mode == "fail")
		{
			await Task.Delay(100, cancellationToken);
			return BaseResponse<ChargeResponse>.Fail("Bank service unavailable");
		}

		if (mode == "timeout")
		{
			await Task.Delay(5000, cancellationToken);
			var timeoutResp = new ChargeResponse(Guid.NewGuid().ToString("N"), request.Request.Amount, request.Request.Currency);
			return BaseResponse<ChargeResponse>.Ok(timeoutResp);
		}

		await Task.Delay(Random.Shared.Next(100, 800), cancellationToken);

		if (Random.Shared.NextDouble() < 0.30)
			return BaseResponse<ChargeResponse>.Fail("Bank returned 503");

		var resp = new ChargeResponse(Guid.NewGuid().ToString("N"), request.Request.Amount, request.Request.Currency);
		return BaseResponse<ChargeResponse>.Ok(resp);
	}
}


