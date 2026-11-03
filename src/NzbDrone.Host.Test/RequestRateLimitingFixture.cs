using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using NzbDrone.Host;

namespace NzbDrone.App.Test
{
    [TestFixture]
    public class RequestRateLimitingFixture
    {
        private static DefaultHttpContext GetContext(string path, string method = "GET", string address = "192.0.2.1")
        {
            var context = new DefaultHttpContext();
            context.Request.Path = path;
            context.Request.Method = method;
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);

            return context;
        }

        [Test]
        public void should_limit_api_bursts_per_client_ip()
        {
            var options = new RateLimiterOptions();
            RequestRateLimiting.Configure(options);
            using var limiter = options.GlobalLimiter;

            var firstClient = GetContext("/api/v3/system/status");
            for (var i = 0; i < 120; i++)
            {
                using var lease = limiter.AttemptAcquire(firstClient);
                lease.IsAcquired.Should().BeTrue();
            }

            using var rejected = limiter.AttemptAcquire(firstClient);
            rejected.IsAcquired.Should().BeFalse();

            using var otherClient = limiter.AttemptAcquire(GetContext("/api/v3/system/status", address: "192.0.2.2"));
            otherClient.IsAcquired.Should().BeTrue();
        }

        [Test]
        public void should_limit_form_login_without_limiting_other_ui_requests()
        {
            var options = new RateLimiterOptions();
            RequestRateLimiting.Configure(options);
            using var limiter = options.GlobalLimiter;

            var login = GetContext("/login", "POST");
            for (var i = 0; i < 10; i++)
            {
                using var lease = limiter.AttemptAcquire(login);
                lease.IsAcquired.Should().BeTrue();
            }

            using var rejected = limiter.AttemptAcquire(login);
            rejected.IsAcquired.Should().BeFalse();

            using var loginPage = limiter.AttemptAcquire(GetContext("/login"));
            loginPage.IsAcquired.Should().BeTrue();

            using var otherClient = limiter.AttemptAcquire(GetContext("/login", "POST", "192.0.2.2"));
            otherClient.IsAcquired.Should().BeTrue();
        }

        [Test]
        public void should_use_connection_address_instead_of_untrusted_forwarded_header()
        {
            var options = new RateLimiterOptions();
            RequestRateLimiting.Configure(options);
            using var limiter = options.GlobalLimiter;

            var request = GetContext("/api/v3/system/status");
            for (var i = 0; i < 120; i++)
            {
                using var lease = limiter.AttemptAcquire(request);
                lease.IsAcquired.Should().BeTrue();
            }

            request.Request.Headers["X-Forwarded-For"] = "192.0.2.2";
            using var rejected = limiter.AttemptAcquire(request);
            rejected.IsAcquired.Should().BeFalse();
        }

        [Test]
        public async Task should_return_429_and_retry_after_when_api_limit_is_exceeded()
        {
            using var services = new ServiceCollection()
                .AddLogging()
                .AddRateLimiter(RequestRateLimiting.Configure)
                .BuildServiceProvider();

            var app = new ApplicationBuilder(services);
            app.UseRateLimiter();
            app.Run(context =>
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                return Task.CompletedTask;
            });

            var pipeline = app.Build();
            for (var i = 0; i < 120; i++)
            {
                var request = GetContext("/api/v3/system/status");
                request.RequestServices = services;
                await pipeline(request);
                request.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
            }

            var rejected = GetContext("/api/v3/system/status");
            rejected.RequestServices = services;
            await pipeline(rejected);

            rejected.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
            rejected.Response.Headers.RetryAfter.ToString().Should().NotBeNullOrEmpty();
            rejected.Response.ContentType.Should().Be("application/json");
        }
    }
}
