using System.Text.Encodings.Web;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using Radarr.Http.Authentication;

namespace NzbDrone.Api.Test.Http.Authentication
{
    [TestFixture]
    public class CalendarFeedAuthenticationFixture
    {
        private const string ApiKey = "0123456789abcdef0123456789abcdef";

        [Test]
        public async Task derived_key_authenticates_only_the_calendar_scheme()
        {
            var feedKey = CalendarFeedCredential.Derive(ApiKey);
            feedKey.Should().NotBe(ApiKey);
            (await AuthenticateCalendar("feedKey=" + feedKey)).Succeeded.Should().BeTrue();
            (await AuthenticateApi("feedKey=" + feedKey)).Succeeded.Should().BeFalse();
        }

        [Test]
        public async Task legacy_api_key_remains_accepted_by_calendar_during_transition()
        {
            (await AuthenticateCalendar("apikey=" + ApiKey)).Succeeded.Should().BeTrue();
            (await AuthenticateCalendar("apikey=wrong")).Succeeded.Should().BeFalse();
            (await AuthenticateCalendar("feedKey=wrong")).Succeeded.Should().BeFalse();
        }

        private static async Task<AuthenticateResult> AuthenticateCalendar(string query)
        {
            var options = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions>>();
            options.Setup(x => x.Get("Calendar")).Returns(new AuthenticationSchemeOptions());
            var config = new Mock<IConfigFileProvider>();
            config.SetupGet(x => x.ApiKey).Returns(ApiKey);
            var handler = new CalendarFeedAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, config.Object);
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?" + query);
            await handler.InitializeAsync(new AuthenticationScheme("Calendar", "Calendar", typeof(CalendarFeedAuthenticationHandler)), context);
            return await handler.AuthenticateAsync();
        }

        private static async Task<AuthenticateResult> AuthenticateApi(string query)
        {
            var options = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<ApiKeyAuthenticationOptions>>();
            options.Setup(x => x.Get("API")).Returns(new ApiKeyAuthenticationOptions { HeaderName = "X-Api-Key", QueryName = "apikey" });
            var config = new Mock<IConfigFileProvider>();
            config.SetupGet(x => x.ApiKey).Returns(ApiKey);
            var handler = new ApiKeyAuthenticationHandler(options.Object, NullLoggerFactory.Instance, UrlEncoder.Default, config.Object);
            var context = new DefaultHttpContext();
            context.Request.QueryString = new QueryString("?" + query);
            await handler.InitializeAsync(new AuthenticationScheme("API", "API", typeof(ApiKeyAuthenticationHandler)), context);
            return await handler.AuthenticateAsync();
        }
    }
}
