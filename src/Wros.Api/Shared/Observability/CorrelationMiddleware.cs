using Serilog.Context;

namespace Wros.Api.Shared.Observability;

public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var supplied = context.Request.Headers[HeaderName].ToString();
        // Only bounded UUIDs enter logs and response headers.
        var correlationId = Guid.TryParse(supplied, out var parsed)
            ? parsed.ToString("D") : Guid.NewGuid().ToString("D");
        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            context.Response.Headers.XContentTypeOptions = "nosniff";
            context.Response.Headers.XFrameOptions = "DENY";
            context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        using (LogContext.PushProperty("CorrelationId", correlationId))
            await next(context);
    }
}
