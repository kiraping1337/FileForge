using System.Globalization;
using System.Threading.RateLimiting;
using FileForge.Application.Errors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FileForge.Api.Middleware;

public sealed class UploadRateLimitOptions
{
    public const string SectionName = "UploadRateLimit";

    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}

public static class UploadRateLimiting
{
    public const string PolicyName = "uploads";

    public static IServiceCollection AddUploadRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<UploadRateLimitOptions>()
            .BindConfiguration(UploadRateLimitOptions.SectionName)
            .Validate(o => o.PermitLimit > 0 && o.Window > TimeSpan.Zero, "Invalid 'UploadRateLimit' settings.")
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // A separate counter per client IP: one client hitting the limit does not block the others.
            // Behind a reverse proxy the real IP comes from X-Forwarded-For; that is set up with Docker.
            options.AddPolicy(PolicyName, httpContext =>
            {
                var settings = httpContext.RequestServices.GetRequiredService<IOptions<UploadRateLimitOptions>>().Value;
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = settings.PermitLimit,
                    Window = settings.Window,
                    QueueLimit = 0,
                });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = "Too many uploads. Try again later.",
                };
                problem.Extensions["code"] = ErrorCodes.RateLimitExceeded;

                var problemDetails = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
                await problemDetails.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails = problem,
                });
            };
        });

        return services;
    }
}
