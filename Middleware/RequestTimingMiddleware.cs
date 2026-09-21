using System.Diagnostics;

namespace QdratNew.Middleware;

public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    private static readonly HashSet<string> StaticExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf"
    };

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        if (StaticExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var ms = sw.ElapsedMilliseconds;

            if (ms >= 1000)
            {
                _logger.LogWarning(
                    "SlowRequest | {Method} {Path} | Status:{StatusCode} | Duration:{DurationMs}ms | Trace:{TraceId}",
                    context.Request.Method,
                    path,
                    context.Response.StatusCode,
                    ms,
                    context.TraceIdentifier);
            }
            else if (ms >= 500)
            {
                _logger.LogInformation(
                    "SlowRequest | {Method} {Path} | Duration:{DurationMs}ms",
                    context.Request.Method, path, ms);
            }
        }
    }
}
