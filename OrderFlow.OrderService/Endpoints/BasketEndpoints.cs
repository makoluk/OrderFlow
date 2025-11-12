using MediatR;
using OrderFlow.OrderService.Features.Basket;
using OrderFlow.Shared.Http;

namespace OrderFlow.OrderService.Endpoints;

public static class BasketEndpoints
{
	public static IEndpointRouteBuilder MapBasketEndpoints(this IEndpointRouteBuilder endpoints)
	{
		var group = endpoints.MapGroup("/basket").WithTags("Basket");

		group.MapGet("/", async (HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			var customerId = GetCustomerId(http);
			var result = await mediator.Send(new GetBasketQuery(customerId), ct);
			return Results.Ok(result);
		})
		.WithName("GetBasket")
		.Produces<BaseResponse<BasketDetailsResponse>>(StatusCodes.Status200OK)
		.WithOpenApi();

		group.MapPost("/items", async (AddBasketItemRequest request, HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			var customerId = GetCustomerId(http);
			var result = await mediator.Send(new AddBasketItemCommand(customerId, request), ct);
			return Results.Json(result, statusCode: result.Success ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest);
		})
		.WithName("AddItemToBasket")
		.Accepts<AddBasketItemRequest>("application/json")
		.Produces<BaseResponse<BasketDetailsResponse>>(StatusCodes.Status200OK)
		.Produces<BaseResponse<BasketDetailsResponse>>(StatusCodes.Status400BadRequest)
		.WithOpenApi();

		group.MapDelete("/items/{productId}", async (string productId, HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			var customerId = GetCustomerId(http);
			var result = await mediator.Send(new RemoveBasketItemCommand(customerId, productId), ct);
			return Results.Ok(result);
		})
		.WithName("RemoveItemFromBasket")
		.Produces<BaseResponse<BasketDetailsResponse>>(StatusCodes.Status200OK)
		.WithOpenApi();

		group.MapDelete("/", async (HttpContext http, IMediator mediator, CancellationToken ct) =>
		{
			var customerId = GetCustomerId(http);
			var result = await mediator.Send(new ClearBasketCommand(customerId), ct);
			return Results.Ok(result);
		})
		.WithName("ClearBasket")
		.Produces<BaseResponse<string>>(StatusCodes.Status200OK)
		.WithOpenApi();

		return endpoints;
	}

	private static string GetCustomerId(HttpContext http)
	{
		// Prefer explicit header; fallback to "anonymous"
		return http.Request.Headers.TryGetValue("X-Customer-Id", out var values)
			? values.ToString()
			: "anonymous";
	}
}


