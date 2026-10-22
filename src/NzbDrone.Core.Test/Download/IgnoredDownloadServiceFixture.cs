using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    // The service reads the neutral release only, so one implementation serves every media type.
    [TestFixture]
    public class IgnoredDownloadServiceFixture : CoreTest<IgnoredDownloadService>
    {
        private static TrackedDownload Tracked(RemoteRelease release)
        {
            return new TrackedDownload
            {
                Kind = release?.Kind,
                RemoteRelease = release,
                DownloadItem = new DownloadClientItem
                {
                    Title = "Release.Title",
                    DownloadId = "HASH",
                    DownloadClientInfo = new DownloadClientItemClientInfo { Id = 3, Name = "qb-main", Type = "QBittorrent" }
                }
            };
        }

        [Test]
        public void should_publish_an_ignored_event_for_a_movie()
        {
            DownloadIgnoredEvent published = null;

            Mocker.GetMock<IEventAggregator>()
                  .Setup(v => v.PublishEvent(It.IsAny<DownloadIgnoredEvent>()))
                  .Callback<DownloadIgnoredEvent>(e => published = e);

            var release = new RemoteMovie
            {
                Movie = new Movie { Id = 9 },
                ParsedMovieInfo = new ParsedMovieInfo { Quality = new QualityModel(Quality.Bluray1080p) },
                Languages = new List<Language> { Language.French }
            };

            var tracked = Tracked(release);

            Subject.IgnoreDownload(tracked).Should().BeTrue();

            published.Should().NotBeNull();
            published.Kind.Should().Be(MediaKind.Movies);
            published.ParentId.Should().Be(9);
            published.Quality.Quality.Should().Be(Quality.Bluray1080p);
            published.Languages.Should().Equal(Language.French);
            published.SourceTitle.Should().Be("Release.Title");
            published.DownloadId.Should().Be("HASH");
            published.DownloadClientInfo.Name.Should().Be("qb-main");
            published.TrackedDownload.Should().BeSameAs(tracked);
            published.Message.Should().Be("Manually ignored");
        }

        [Test]
        public void should_publish_an_ignored_event_for_another_media_type()
        {
            DownloadIgnoredEvent published = null;

            Mocker.GetMock<IEventAggregator>()
                  .Setup(v => v.PublishEvent(It.IsAny<DownloadIgnoredEvent>()))
                  .Callback<DownloadIgnoredEvent>(e => published = e);

            var release = new TvRemoteRelease
            {
                Series = new TvParent { Id = 42 },
                ParsedQuality = new QualityModel(Quality.HDTV720p)
            };

            Subject.IgnoreDownload(Tracked(release)).Should().BeTrue();

            published.Should().NotBeNull();
            published.Kind.Should().Be(MediaKind.Tv);
            published.ParentId.Should().Be(42);
            published.Quality.Quality.Should().Be(Quality.HDTV720p);
        }

        [Test]
        public void should_not_ignore_a_download_with_no_matched_library_entry()
        {
            var release = new RemoteMovie { Movie = null };

            Subject.IgnoreDownload(Tracked(release)).Should().BeFalse();

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadIgnoredEvent>()), Times.Never());
        }

        [Test]
        public void should_not_ignore_a_download_with_no_release()
        {
            Subject.IgnoreDownload(Tracked(null)).Should().BeFalse();

            Mocker.GetMock<IEventAggregator>()
                  .Verify(v => v.PublishEvent(It.IsAny<DownloadIgnoredEvent>()), Times.Never());
        }
    }
}
