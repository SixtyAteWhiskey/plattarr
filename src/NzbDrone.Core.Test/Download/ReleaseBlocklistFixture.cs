using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class ReleaseBlocklistFixture
    {
        private Mock<IMediaBlocklist> _movies;
        private Mock<IMediaBlocklist> _tv;
        private ReleaseBlocklist _subject;

        [SetUp]
        public void Setup()
        {
            _movies = new Mock<IMediaBlocklist>();
            _movies.SetupGet(b => b.Kind).Returns(MediaKind.Movies);
            _tv = new Mock<IMediaBlocklist>();
            _tv.SetupGet(b => b.Kind).Returns(MediaKind.Tv);

            _subject = new ReleaseBlocklist(new List<IMediaBlocklist> { _movies.Object, _tv.Object });
        }

        [Test]
        public void should_ask_the_module_that_owns_the_release()
        {
            var release = new TvRemoteRelease();

            _tv.Setup(b => b.IsTorrentHashBlocklisted(release, "hash")).Returns(true);

            _subject.IsTorrentHashBlocklisted(release, "hash").Should().BeTrue();

            _movies.Verify(b => b.IsTorrentHashBlocklisted(It.IsAny<RemoteRelease>(), It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_not_block_when_no_module_owns_the_release()
        {
            var subject = new ReleaseBlocklist(new List<IMediaBlocklist> { _movies.Object });

            subject.IsTorrentHashBlocklisted(new TvRemoteRelease(), "hash").Should().BeFalse();
        }
    }
}
