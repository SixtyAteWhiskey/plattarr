using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;
using Radarr.Api.V3.System;
using Radarr.Http.Middleware;

namespace NzbDrone.Api.Test.v3.SystemApi
{
    [TestFixture]
    public class SystemControllerCompatibilityFixture : TestBase<SystemController>
    {
        [TestCase(SeerrCompatibilityMiddleware.RadarrApplication, "Radarr", false)]
        [TestCase(SeerrCompatibilityMiddleware.SonarrApplication, "Sonarr", true)]
        public void should_report_the_servarr_identity_only_for_compatibility_requests(
            string application,
            string expectedName,
            bool isSonarr)
        {
            var context = new DefaultHttpContext();
            context.Items[SeerrCompatibilityMiddleware.ApplicationItemKey] = application;
            Subject.ControllerContext = new ControllerContext { HttpContext = context };

            var status = Subject.GetStatus().Value;

            status.AppName.Should().Be(expectedName);
            if (isSonarr)
            {
                status.Version.Should().StartWith("4.");
            }
            else
            {
                status.Version.Should().Be(BuildInfo.Version.ToString());
            }
        }
    }
}
