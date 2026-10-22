using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Downloads;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.FailedDownloadServiceTests
{
    [TestFixture]
    public class MovieFailedDownloadSourceFixture : CoreTest<MovieFailedDownloadSource>
    {
        private static MovieHistory Grab(int id, string downloadId, DateTime date, int movieId = 9)
        {
            var history = new MovieHistory
            {
                Id = id,
                MediaKind = MediaKind.Movies,
                EventType = MovieHistoryEventType.Grabbed,
                ParentId = movieId,
                DownloadId = downloadId,
                SourceTitle = "Movie.2020.1080p.WEB-DL",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<Language> { Language.French },
                Date = date
            };

            history.Data[MovieHistory.DOWNLOAD_CLIENT] = "QBittorrent";
            history.Data[MovieHistory.RELEASE_SOURCE] = ReleaseSourceType.InteractiveSearch.ToString();
            history.Data["indexer"] = "Knaben";

            return history;
        }

        private void GivenGrabs(params MovieHistory[] grabs)
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Find("HASH", MovieHistoryEventType.Grabbed))
                  .Returns(new List<MovieHistory>(grabs));
        }

        [Test]
        public void should_be_the_movie_source()
        {
            Subject.Kind.Should().Be(MediaKind.Movies);
        }

        [Test]
        public void should_report_a_grab_when_history_has_one()
        {
            GivenGrabs(Grab(1, "HASH", DateTime.UtcNow));

            Subject.WasGrabbed("HASH").Should().BeTrue();
        }

        [Test]
        public void should_report_no_grab_when_history_has_none()
        {
            GivenGrabs();

            Subject.WasGrabbed("HASH").Should().BeFalse();
        }

        [Test]
        public void should_describe_nothing_when_history_has_no_grab()
        {
            GivenGrabs();

            Subject.Describe("HASH", "failed", new TrackedDownload(), false).Should().BeNull();
        }

        [Test]
        public void should_describe_the_failure_from_the_newest_grab()
        {
            var older = Grab(1, "HASH", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), 3);
            var newer = Grab(2, "HASH", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc), 9);
            GivenGrabs(older, newer);

            var trackedDownload = new TrackedDownload();

            var failure = Subject.Describe("HASH", "Disk full", trackedDownload, true);

            failure.Kind.Should().Be(MediaKind.Movies);
            failure.ParentId.Should().Be(9);
            failure.Quality.Should().Be(newer.Quality);
            failure.Languages.Should().Equal(Language.French);
            failure.SourceTitle.Should().Be("Movie.2020.1080p.WEB-DL");
            failure.DownloadClient.Should().Be("QBittorrent");
            failure.DownloadId.Should().Be("HASH");
            failure.Message.Should().Be("Disk full");
            failure.ReleaseSource.Should().Be(ReleaseSourceType.InteractiveSearch);
            failure.Data.Should().ContainKey("indexer");
            failure.TrackedDownload.Should().BeSameAs(trackedDownload);
            failure.SkipRedownload.Should().BeTrue();
        }

        [Test]
        public void should_read_an_unknown_release_source_when_history_has_none()
        {
            var grab = Grab(1, "HASH", DateTime.UtcNow);
            grab.Data.Remove(MovieHistory.RELEASE_SOURCE);
            GivenGrabs(grab);

            Subject.Describe("HASH", "failed", null, false).ReleaseSource.Should().Be(ReleaseSourceType.Unknown);
        }

        [Test]
        public void history_row_without_a_download_id_should_honor_skip_redownload()
        {
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Get(7))
                  .Returns(Grab(7, null, DateTime.UtcNow));

            Subject.Describe(7, "Manually marked as failed", true).SkipRedownload.Should().BeTrue();
        }

        [Test]
        public void history_row_with_a_download_id_should_not_honor_skip_redownload()
        {
            // Upstream behavior, kept: the skip flag only counts for a history row with no download id.
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.Get(7))
                  .Returns(Grab(7, "HASH", DateTime.UtcNow));

            var failure = Subject.Describe(7, "Manually marked as failed", true);

            failure.SkipRedownload.Should().BeFalse();
            failure.TrackedDownload.Should().BeNull();
            failure.ParentId.Should().Be(9);
            failure.Kind.Should().Be(MediaKind.Movies);
        }
    }
}
