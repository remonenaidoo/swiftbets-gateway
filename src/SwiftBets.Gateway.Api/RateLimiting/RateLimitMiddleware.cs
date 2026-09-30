using System.Globalization;
using SwiftBets.BuildingBlocks.Web;
using SwiftBets.Gateway.Application.RateLimiting;
using SwiftBets.Gateway.Domain;

namespace SwiftBets.Gateway.Api.RateLimiting;

public sealed class RateLimitMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IRateLimitCounter counter)
    {
        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var policy = RateLimitPolicy.For(context.Request.Path, context.Request.Method);
        var partition = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var decision = await counter.HitAsync(policy, partition, context.RequestAborted);
        context.Response.Headers["RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);
        if (decision.Allowed)
        {
            await next(context);
            return;
        }

        context.Response.Headers.RetryAfter = Math.Ceiling(decision.RetryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await ErrorEnvelopes.WriteAsync(context, ErrorEnvelopes.Create(context, StatusCodes.Status429TooManyRequests, "rate_limited", $"Limit for {policy.Name} reached."));
    }
}
