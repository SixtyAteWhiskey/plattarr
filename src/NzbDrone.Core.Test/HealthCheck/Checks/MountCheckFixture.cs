using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class MountCheckFixture : CoreTest<MountCheck>
    {
        private const string Guidance = "A mount containing a movie path reports read-only. Mount settings or a filesystem issue may be responsible. Check the movie root folders and mount status on the host, resolve the cause, then rerun System Health before retrying imports.";

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.AllMoviePaths())
                  .Returns(new Dictionary<int, string> { { 1, "/private/library/Movie" } });

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString("MountMovieReadOnlyHealthCheckMessage"))
                  .Returns(Guidance);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_report_only_read_only_mounts_without_private_details(bool readOnly)
        {
            var mount = new Mock<IMount>();
            mount.SetupGet(m => m.MountOptions).Returns(new MountOptions(new Dictionary<string, string>
            {
                { readOnly ? "ro" : "rw", string.Empty }
            }));
            mount.SetupGet(m => m.Name).Returns("private-volume");
            mount.SetupGet(m => m.RootDirectory).Returns("/private/library");

            Mocker.GetMock<IDiskProvider>()
                  .Setup(s => s.GetMount("/private/library/Movie"))
                  .Returns(mount.Object);

            var result = Subject.Check();

            if (readOnly)
            {
                result.ShouldBeError(wikiFragment: "movies-mount-ro");
                result.Message.Should().Be(Guidance)
                      .And.NotContain("private-volume")
                      .And.NotContain("/private/library");
            }
            else
            {
                result.ShouldBeOk();
            }
        }
    }
}
