using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OrderFlow.Shared.Http;

namespace OrderFlow.Shared.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var traceId = context.TraceIdentifier;
            var correlationId = Activity.Current?.TraceId.ToString() ?? traceId;

            _logger.LogError(ex, "Unhandled exception | TraceId={TraceId} CorrelationId={CorrelationId}", traceId, correlationId);

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var error = BaseResponse<string>.Fail("An unexpected error occurred.", ex.Message);
            error.TraceId = traceId;

            await context.Response.WriteAsJsonAsync(error);
        }
    }
}


