using System.Net;
using System.Text.Json;

namespace LedgeLink.Distributor.API.API.Middleware;

/// <summary>
/// API layer: catches unhandled exceptions and returns a consistent JSON error envelope.
/// Keeps controllers clean — no try/catch blocks needed in business code.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next   = next;
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
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode  = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";

            var body = JsonSerializer.Serialize(new
            {
                error     = "An unexpected error occurred.",
                requestId = context.TraceIdentifier
            });

            await context.Response.WriteAsync(body);
        }
    }
}
