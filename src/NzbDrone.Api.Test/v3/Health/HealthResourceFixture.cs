using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Health;

namespace NzbDrone.Api.Test.v3.Health
{
    [TestFixture]
    public class HealthResourceFixture : TestBase
    {
        [Test]
        public void should_serialize_safe_mount_failure_message()
        {
            const string guidance = "A mount containing a movie path reports read-only. Mount settings or a filesystem issue may be responsible. Check the movie root folders and mount status on the host, resolve the cause, then rerun System Health before retrying imports.";
            var movies = new Mock<IMovieService>();
            movies.Setup(s => s.AllMoviePaths()).Returns(new Dictionary<int, string> { { 1, "/private/library/Movie" } });

            var mount = new Mock<IMount>();
            mount.SetupGet(m => m.MountOptions).Returns(new MountOptions(new Dictionary<string, string> { { "ro", string.Empty } }));
            mount.SetupGet(m => m.Name).Returns("private-volume");
            mount.SetupGet(m => m.RootDirectory).Returns("/private/library");

            var disk = new Mock<IDiskProvider>();
            disk.Setup(s => s.GetMount("/private/library/Movie")).Returns(mount.Object);

            var localization = new Mock<ILocalizationService>();
            localization.Setup(s => s.GetLocalizedString("MountMovieReadOnlyHealthCheckMessage")).Returns(guidance);

            var check = new MountCheck(disk.Object, movies.Object, localization.Object).Check();

            var resource = check.ToResource();
            var json = STJson.ToJson(resource);

            resource.Source.Should().Be(nameof(MountCheck));
            resource.Type.Should().Be(HealthCheckResult.Error);
            resource.Message.Should().Be(guidance);
            resource.WikiUrl.Should().Contain("#movies-mount-ro");
            json.Should().Contain("mount status on the host")
                .And.NotContain("private-volume")
                .And.NotContain("/private/library");
        }
    }
}
