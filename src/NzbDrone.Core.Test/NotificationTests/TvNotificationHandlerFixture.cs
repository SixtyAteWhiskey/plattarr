using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.MediaFiles.Events;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.RootFolders;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class TvNotificationHandlerFixture : TestBase
    {
        private Mock<INotificationDispatcher> _dispatcher;
        private TvNotificationHandler _handler;
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _dispatcher = new Mock<INotificationDispatcher>();
            _dispatcher.Setup(d => d.AnyEnabled(It.IsAny<NotificationEvent>())).Returns(true);

            var renderer = new TvActivityRenderer(new Mock<ISeriesService>().Object, new Mock<IEpisodeService>().Object, new Mock<IRootFolderService>().Object);
            _handler = new TvNotificationHandler(_dispatcher.Object, renderer);

            _series = TvGoldenData.Series();
            _episodes = TvGoldenData.Episodes(_series);
        }

        [Test]
        public void should_not_notify_for_a_grab_of_another_media_type()
        {
            _handler.Handle(new ReleaseGrabbedEvent(new RemoteMovie { Release = new ReleaseInfo { Title = "Movie.2020" } }) { DownloadId = "HASH" });

            _dispatcher.Verify(d => d.Grab(It.IsAny<GrabMessage>()), Times.Never());
        }

        [Test]
        public void should_notify_a_grab_with_the_message_sonarr_sends()
        {
            GrabMessage sent = null;
            _dispatcher.Setup(d => d.Grab(It.IsAny<GrabMessage>())).Callback<GrabMessage>(m => sent = m);

            _handler.Handle(new ReleaseGrabbedEvent(TvGoldenData.Remote(_series, _episodes)) { DownloadClient = "qBittorrent", DownloadClientName = "qb", DownloadId = "HASH" });

            sent.Message.Should().Be("Game of Thrones - 1x01x02 - Winter Is Coming + The Kingsroad [WEBDL-1080p Proper]");
            sent.QualityName.Should().Be("WEBDL-1080p");
            sent.DownloadId.Should().Be("HASH");
            sent.Subject.Kind.Should().Be(MediaKind.Tv);
            sent.Data.Should().BeOfType<TvNotificationData>().Which.Series.Should().BeSameAs(_series);
            sent.Release.Title.Should().Be("Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
        }

        [Test]
        public void should_build_nothing_when_no_provider_wants_the_event()
        {
            _dispatcher.Setup(d => d.AnyEnabled(It.IsAny<NotificationEvent>())).Returns(false);

            _handler.Handle(new ReleaseGrabbedEvent(TvGoldenData.Remote(_series, _episodes)));
            _handler.Handle(new SeriesAddCompletedEvent(_series));
            _handler.Handle(new SeriesDeletedEvent(new List<Series> { _series }, true, false));

            _dispatcher.Verify(d => d.Grab(It.IsAny<GrabMessage>()), Times.Never());
            _dispatcher.Verify(d => d.MediaAdded(It.IsAny<MediaAddedMessage>()), Times.Never());
            _dispatcher.Verify(d => d.MediaDelete(It.IsAny<MediaDeleteMessage>()), Times.Never());
        }

        [Test]
        public void should_not_notify_an_import_that_is_not_a_new_download()
        {
            var file = TvGoldenData.EpisodeFile(_series, _episodes);

            _handler.Handle(new EpisodeImportedEvent(TvGoldenData.Local(_series, _episodes), file, new List<DeletedEpisodeFile>(), false, null));

            _dispatcher.Verify(d => d.Download(It.IsAny<DownloadMessage>()), Times.Never());
        }

        [Test]
        public void should_report_an_import_that_replaced_a_file_as_an_upgrade()
        {
            DownloadMessage sent = null;
            _dispatcher.Setup(d => d.Download(It.IsAny<DownloadMessage>())).Callback<DownloadMessage>(m => sent = m);

            var file = TvGoldenData.EpisodeFile(_series, _episodes);
            var old = new DeletedEpisodeFile(TvGoldenData.EpisodeFile(_series, _episodes, 9), "/recycle/old.mkv");

            _handler.Handle(new EpisodeImportedEvent(TvGoldenData.Local(_series, _episodes), file, new List<DeletedEpisodeFile> { old }, true, null));

            sent.IsUpgrade.Should().BeTrue();
            sent.OldFiles.Should().ContainSingle();
            sent.File.Id.Should().Be(10);
            sent.CustomFormatScore.Should().Be(75);
        }

        [Test]
        public void should_not_notify_a_deleted_file_with_no_episodes()
        {
            var file = TvGoldenData.EpisodeFile(_series, new List<Episode>());

            _handler.Handle(new EpisodeFileDeletedEvent(file, DeleteMediaFileReason.Manual));

            _dispatcher.Verify(d => d.MediaFileDelete(It.IsAny<MediaFileDeleteMessage>()), Times.Never());
        }

        [Test]
        public void should_send_one_delete_message_per_series()
        {
            var other = TvGoldenData.Series();
            other.Id = 2;
            other.Title = "Another";

            _handler.Handle(new SeriesDeletedEvent(new List<Series> { _series, other }, true, false));

            _dispatcher.Verify(d => d.MediaDelete(It.IsAny<MediaDeleteMessage>()), Times.Exactly(2));
        }

        [Test]
        public void should_not_notify_manual_interaction_published_by_another_media_type()
        {
            var tracked = TvGoldenData.Tracked(TvGoldenData.Remote(_series, _episodes));

            _handler.Handle(new ManualInteractionRequiredEvent(tracked, new NzbDrone.Core.Movies.Notifications.MovieNotificationData()));

            _dispatcher.Verify(d => d.ManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>()), Times.Never());
        }

        [Test]
        public void should_use_the_download_title_for_manual_interaction_on_an_unmatched_download()
        {
            ManualInteractionRequiredMessage sent = null;
            _dispatcher.Setup(d => d.ManualInteractionRequired(It.IsAny<ManualInteractionRequiredMessage>())).Callback<ManualInteractionRequiredMessage>(m => sent = m);

            var tracked = TvGoldenData.Tracked(null);

            _handler.Handle(new ManualInteractionRequiredEvent(tracked, new TvNotificationData()));

            sent.Message.Should().Be("Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
            sent.Subject.Should().BeNull();
        }

        [TestCase(SeriesTypes.Standard, 1, "Show - 1x01 - Pilot [HDTV-720p]")]
        [TestCase(SeriesTypes.Standard, 2, "Show - 1x01 - Pilot [HDTV-720p Proper]")]
        [TestCase(SeriesTypes.Anime, 2, "Show - 1x01 - Pilot [HDTV-720p v2]")]
        [TestCase(SeriesTypes.Daily, 1, "Show - 2020-01-02 - Pilot [HDTV-720p]")]
        public void should_format_the_message_by_series_type_and_revision(SeriesTypes type, int version, string expected)
        {
            var series = new Series { Title = "Show", SeriesType = type };
            var episodes = new List<Episode> { new Episode { SeasonNumber = 1, EpisodeNumber = 1, Title = "Pilot", AirDate = "2020-01-02" } };

            TvNotificationHandler.GetMessage(series, episodes, new QualityModel(Quality.HDTV720p, new Revision(version))).Should().Be(expected);
        }

        [Test]
        public void should_format_a_message_without_episodes()
        {
            TvNotificationHandler.GetMessage(new Series { Title = "Show" }, new List<Episode>(), new QualityModel(Quality.HDTV720p))
                .Should().Be("Show - [HDTV-720p]");
        }
    }
}
