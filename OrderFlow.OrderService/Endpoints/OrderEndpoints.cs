using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderFlow.OrderService.Features.Orders;
using OrderFlow.Shared.Http;
using OrderFlow.OrderService.Data;
using MassTransit;
using OrderFlow.Shared.Contracts;

namespace OrderFlow.OrderService.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/orders", async ([FromServices] IMediator mediator, HttpContext httpContext, [FromBody] CreateOrderRequest req) =>
        {
            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
            var cmd = new CreateOrderCommand(req.Amount, req.Currency, req.CustomerId, idempotencyKey);
            return await mediator.Send(cmd);
        })
        .WithName("CreateOrder")
        .WithTags("Orders")
        .Accepts<CreateOrderRequest>("application/json")
        .Produces<BaseResponse<CreateOrderResponse>>(StatusCodes.Status201Created)
        .Produces<BaseResponse<string>>(StatusCodes.Status400BadRequest)
        .Produces<BaseResponse<string>>(StatusCodes.Status500InternalServerError)
        .WithOpenApi();

        app.MapGet("/orders/{id:guid}", async ([FromServices] IMediator mediator, [FromRoute] Guid id) =>
        {
            return await mediator.Send(new GetOrderQuery(id));
        })
        .WithName("GetOrderById")
        .WithTags("Orders")
        .Produces<BaseResponse<OrderDetailsResponse>>(StatusCodes.Status200OK)
        .Produces<BaseResponse<string>>(StatusCodes.Status404NotFound)
        .WithOpenApi();

        app.MapPost("/orders/checkout", async ([FromServices] IMediator mediator, HttpContext httpContext) =>
        {
            var customerId = httpContext.Request.Headers.TryGetValue("X-Customer-Id", out var cid)
                ? cid.ToString()
                : "anonymous";

            var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].ToString();
            var cmd = new CreateOrderFromBasketCommand(customerId, idempotencyKey);
            return await mediator.Send(cmd);
        })
        .WithName("CheckoutOrderFromBasket")
        .WithTags("Orders")
        .Produces<BaseResponse<CreateOrderResponse>>(StatusCodes.Status201Created)
        .Produces<BaseResponse<string>>(StatusCodes.Status400BadRequest)
        .Produces<BaseResponse<string>>(StatusCodes.Status500InternalServerError)
        .WithOpenApi();

        // Reconcile endpoint to emit OrderCompleted if all steps exist but completion was missed
        app.MapPost("/orders/{id:guid}/reconcile", async (
            [FromRoute] Guid id,
            [FromServices] OrderDbContext db,
            [FromServices] IPublishEndpoint bus) =>
        {
            var order = await db.Orders.FindAsync(id);
            if (order is null)
                return Results.NotFound(BaseResponse<string>.Fail("Order not found"));

            if (order.CompletedAtUtc.HasValue)
                return Results.Ok(BaseResponse<string>.Ok("Already completed"));

            if (order.PaidAtUtc.HasValue && order.StockReservedAtUtc.HasValue && order.EmailSentAtUtc.HasValue)
            {
                await bus.Publish(new OrderCompleted(order.Id, DateTime.UtcNow));
                return Results.Ok(BaseResponse<string>.Ok("OrderCompleted published"));
            }

            return Results.BadRequest(BaseResponse<string>.Fail("Order not ready for completion"));
        })
        .WithName("ReconcileOrder")
        .WithTags("Orders")
        .Produces<BaseResponse<string>>(StatusCodes.Status200OK)
        .Produces<BaseResponse<string>>(StatusCodes.Status400BadRequest)
        .Produces<BaseResponse<string>>(StatusCodes.Status404NotFound)
        .WithOpenApi();

        return app;
    }
}


