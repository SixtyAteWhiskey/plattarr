using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Blocklisting
{
    [TestFixture]
    public class MovieBlocklistFixture : CoreTest<MovieBlocklist>
    {
        [Test]
        public void should_be_the_movie_blocklist()
        {
            Subject.Kind.Should().Be(MediaKind.Movies);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void should_check_the_hash_against_the_movie(bool blocklisted)
        {
            var release = new RemoteMovie { Movie = new Movie { Id = 42 } };

            Mocker.GetMock<IBlocklistService>()
                .Setup(s => s.BlocklistedTorrentHash(42, "hash"))
                .Returns(blocklisted);

            Subject.IsTorrentHashBlocklisted(release, "hash").Should().Be(blocklisted);
        }

        [Test]
        public void should_not_block_a_release_that_matched_no_movie()
        {
            Subject.IsTorrentHashBlocklisted(new RemoteMovie(), "hash").Should().BeFalse();

            Mocker.GetMock<IBlocklistService>()
                .Verify(s => s.BlocklistedTorrentHash(It.IsAny<int>(), It.IsAny<string>()), Times.Never());
        }
    }
}
