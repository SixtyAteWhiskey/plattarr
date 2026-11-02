using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.Parser.Model;
using MusicBlocklistService = NzbDrone.Core.Music.Blocklisting.IBlocklistService;
using TvBlocklistService = NzbDrone.Core.Tv.Blocklisting.IBlocklistService;

namespace NzbDrone.Core.Test.Blocklisting
{
    [TestFixture]
    public class MediaBlocklistAdaptersFixture
    {
        [Test]
        public void tv_adapter_checks_only_the_matched_series()
        {
            var service = new Mock<TvBlocklistService>();
            var adapter = new TvBlocklist(service.Object);
            var release = new RemoteEpisode { Series = new Series { Id = 42 } };

            adapter.Kind.Should().Be(MediaKind.Tv);
            service.Setup(s => s.BlocklistedTorrentHash(42, "hash")).Returns(true);
            adapter.IsTorrentHashBlocklisted(release, "hash").Should().BeTrue();
            adapter.IsTorrentHashBlocklisted(new RemoteEpisode(), "hash").Should().BeFalse();
            service.Verify(s => s.BlocklistedTorrentHash(42, "hash"), Times.Once());
            service.VerifyNoOtherCalls();
        }

        [Test]
        public void music_adapter_checks_only_the_matched_artist()
        {
            var service = new Mock<MusicBlocklistService>();
            var adapter = new MusicBlocklist(service.Object);
            var release = new RemoteAlbum { Artist = new Artist { Id = 84 } };

            adapter.Kind.Should().Be(MediaKind.Music);
            service.Setup(s => s.BlocklistedTorrentHash(84, "hash")).Returns(true);
            adapter.IsTorrentHashBlocklisted(release, "hash").Should().BeTrue();
            adapter.IsTorrentHashBlocklisted(new RemoteAlbum(), "hash").Should().BeFalse();
            service.Verify(s => s.BlocklistedTorrentHash(84, "hash"), Times.Once());
            service.VerifyNoOtherCalls();
        }
    }
}
