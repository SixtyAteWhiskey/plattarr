using System;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Radarr.Http.Extensions;

namespace NzbDrone.Host
{
    public static class RequestRateLimiting
    {
        private const int ApiBurst = 120;
        private const int ApiTokensPerSecond = 10;
        private const int LoginAttemptsPerMinute = 10;

        public static void Configure(RateLimiterOptions options)
        {
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var request = context.Request;
                var address = context.Connection.RemoteIpAddress;

                if (address?.IsIPv4MappedToIPv6 == true)
                {
                    address = address.MapToIPv4();
                }

                var client = address?.ToString() ?? "unknown";

                if (request.IsApiRequest())
                {
                    return RateLimitPartition.GetTokenBucketLimiter($"api:{client}", _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = ApiBurst,
                        TokensPerPeriod = ApiTokensPerSecond,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                        QueueLimit = 0
                    });
                }

                if (HttpMethods.IsPost(request.Method) &&
                    string.Equals(request.Path.Value, "/login", StringComparison.OrdinalIgnoreCase))
                {
                    return RateLimitPartition.GetFixedWindowLimiter($"login:{client}", _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = LoginAttemptsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    });
                }

                return RateLimitPartition.GetNoLimiter("other");
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
                }

                if (context.HttpContext.Request.IsApiRequest())
                {
                    response.ContentType = "application/json";
                    await response.WriteAsync("{\"message\":\"Too many requests. Try again later.\"}", cancellationToken);
                }
                else
                {
                    response.ContentType = "text/plain";
                    await response.WriteAsync("Too many login attempts. Try again later.", cancellationToken);
                }
            };
        }
    }
}
