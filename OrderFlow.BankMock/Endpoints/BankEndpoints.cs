using MediatR;
using OrderFlow.BankMock.Features.Payments;
using OrderFlow.Shared.Http;

namespace OrderFlow.BankMock.Endpoints;

public static class BankEndpoints
{
	public static IEndpointRouteBuilder MapBankEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/").WithTags("Bank");

		group.MapPost("/charge", async (ChargeRequest req, string? mode, IMediator mediator, CancellationToken ct) =>
		{
			var result = await mediator.Send(new ChargeCommand(req, mode), ct);
			return Results.Json(result, statusCode: result.Success ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
		})
		.WithName("Charge")
		.Accepts<ChargeRequest>("application/json")
		.Produces<BaseResponse<ChargeResponse>>(StatusCodes.Status200OK)
		.Produces<BaseResponse<string>>(StatusCodes.Status503ServiceUnavailable)
		.WithOpenApi();

		group.MapPost("/refund", async (RefundRequest req, string? mode, IMediator mediator, CancellationToken ct) =>
		{
			var result = await mediator.Send(new RefundCommand(req, mode), ct);
			return Results.Json(result, statusCode: result.Success ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
		})
		.WithName("Refund")
		.Accepts<RefundRequest>("application/json")
		.Produces<BaseResponse<RefundResponse>>(StatusCodes.Status200OK)
		.Produces<BaseResponse<string>>(StatusCodes.Status503ServiceUnavailable)
		.WithOpenApi();

		return endpoints;
	}
}


