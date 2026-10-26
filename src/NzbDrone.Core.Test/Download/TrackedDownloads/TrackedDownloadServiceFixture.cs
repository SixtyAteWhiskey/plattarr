using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.Download.TrackedDownloads
{
    // Contract 5 routing: the service decides the media type of a download (grab history, then the
    // client category, then the title) and hands it to that media type's matcher. A movie matcher and
    // a TV matcher stand in for two modules.
    [TestFixture]
    public class TrackedDownloadServiceFixture : CoreTest<TrackedDownloadService>
    {
        private Mock<ITrackedDownloadMatcher> _movieMatcher;
        private Mock<ITrackedDownloadMatcher> _tvMatcher;

        private class CategorySettings : IProviderConfig, IModuleCategorySettings
        {
            public List<ModuleCategorySettings> ModuleCategories { get; set; } = new();

            public NzbDroneValidationResult Validate()
            {
                return new NzbDroneValidationResult();
            }
        }

        [SetUp]
        public void Setup()
        {
            _movieMatcher = GivenMatcher(MediaKind.Movies, false);
            _tvMatcher = GivenMatcher(MediaKind.Tv, false);

            Mocker.SetConstant<IEnumerable<ITrackedDownloadMatcher>>(new[] { _movieMatcher.Object, _tvMatcher.Object });
        }

        private static Mock<ITrackedDownloadMatcher> GivenMatcher(MediaKind kind, bool matchesTitle)
        {
            var matcher = new Mock<ITrackedDownloadMatcher>();
            matcher.SetupGet(m => m.Kind).Returns(kind);
            matcher.Setup(m => m.MatchesTitle(It.IsAny<string>())).Returns(matchesTitle);
            matcher.Setup(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()))
                   .Callback<TrackedDownload, DownloadHistory>((t, h) => t.RemoteRelease = new TvRemoteRelease());

            return matcher;
        }

        private void GivenSingleMatcher(Mock<ITrackedDownloadMatcher> matcher)
        {
            Mocker.SetConstant<IEnumerable<ITrackedDownloadMatcher>>(new[] { matcher.Object });
        }

        private static DownloadClientDefinition Client(params (MediaKind Kind, string Category)[] categories)
        {
            var settings = new CategorySettings();

            foreach (var (kind, category) in categories)
            {
                settings.ModuleCategories.Add(new ModuleCategorySettings { Kind = kind, Category = category });
            }

            return new DownloadClientDefinition { Id = 1, Protocol = DownloadProtocol.Torrent, Settings = settings };
        }

        private static DownloadClientItem Item(string title = "Something 2020", string category = null, string downloadId = "35238")
        {
            return new DownloadClientItem
            {
                Title = title,
                Category = category,
                DownloadId = downloadId,
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = DownloadProtocol.Torrent,
                    Id = 1,
                    Name = "Client"
                }
            };
        }

        private void GivenDownloadHistory(MediaKind kind, DownloadHistoryEventType eventType = DownloadHistoryEventType.DownloadGrabbed)
        {
            Mocker.GetMock<IDownloadHistoryService>()
                  .Setup(s => s.GetLatestDownloadHistoryItem("35238"))
                  .Returns(new DownloadHistory { MediaKind = kind, EventType = eventType, ParentId = 5 });
        }

        [Test]
        public void should_route_by_the_download_history_first()
        {
            GivenDownloadHistory(MediaKind.Tv);

            // The category and the title both say movies; the grab history wins.
            _movieMatcher.Setup(m => m.MatchesTitle(It.IsAny<string>())).Returns(true);

            var tracked = Subject.TrackDownload(Client((MediaKind.Movies, "movies")), Item(category: "movies"));

            tracked.Kind.Should().Be(MediaKind.Tv);
            _tvMatcher.Verify(m => m.Match(tracked, It.IsAny<DownloadHistory>()), Times.Once());
            _movieMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Never());
        }

        [Test]
        public void changing_a_grabbed_download_category_should_not_transfer_ownership()
        {
            GivenDownloadHistory(MediaKind.Movies);

            var client = Client((MediaKind.Movies, "movies"), (MediaKind.Tv, "tv"));
            var before = Subject.TrackDownload(client, Item(category: "movies"));
            var after = Subject.TrackDownload(client, Item(category: "tv"));

            before.Kind.Should().Be(MediaKind.Movies);
            after.Kind.Should().Be(MediaKind.Movies);
            _movieMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Exactly(2));
            _tvMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Never());
        }

        [Test]
        public void should_pass_the_download_history_to_the_matcher()
        {
            GivenDownloadHistory(MediaKind.Movies, DownloadHistoryEventType.DownloadImported);

            var tracked = Subject.TrackDownload(Client(), Item());

            _movieMatcher.Verify(m => m.Match(tracked, It.Is<DownloadHistory>(h => h.EventType == DownloadHistoryEventType.DownloadImported && h.ParentId == 5)), Times.Once());
            tracked.State.Should().Be(TrackedDownloadState.Imported);
        }

        [Test]
        public void should_route_by_the_client_category_without_history()
        {
            var tracked = Subject.TrackDownload(Client((MediaKind.Movies, "plattarr-movies"), (MediaKind.Tv, "plattarr-tv")), Item(category: "PLATTARR-TV"));

            tracked.Kind.Should().Be(MediaKind.Tv);
            _tvMatcher.Verify(m => m.Match(tracked, null), Times.Once());
        }

        [Test]
        public void should_prefer_the_category_over_the_title()
        {
            _movieMatcher.Setup(m => m.MatchesTitle(It.IsAny<string>())).Returns(true);

            var tracked = Subject.TrackDownload(Client((MediaKind.Tv, "tv")), Item(category: "tv"));

            tracked.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_route_by_the_title_when_exactly_one_media_type_matches_it()
        {
            _tvMatcher.Setup(m => m.MatchesTitle("Some Show S01E02")).Returns(true);

            var tracked = Subject.TrackDownload(Client((MediaKind.Movies, "movies")), Item("Some Show S01E02", category: "unrelated"));

            tracked.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_leave_the_kind_empty_when_several_media_types_match_the_title()
        {
            _movieMatcher.Setup(m => m.MatchesTitle(It.IsAny<string>())).Returns(true);
            _tvMatcher.Setup(m => m.MatchesTitle(It.IsAny<string>())).Returns(true);

            var tracked = Subject.TrackDownload(Client(), Item());

            tracked.Kind.Should().BeNull();
            tracked.RemoteRelease.Should().BeNull();
            tracked.IsTrackable.Should().BeTrue();
            _movieMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Never());
            _tvMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Never());
        }

        [Test]
        public void should_leave_the_kind_empty_when_nothing_matches_and_there_is_more_than_one_media_type()
        {
            var tracked = Subject.TrackDownload(Client(), Item());

            tracked.Kind.Should().BeNull();
            Subject.GetTrackedDownloads().Should().Contain(tracked);
        }

        [Test]
        public void should_give_an_unattributed_download_to_the_only_media_type()
        {
            GivenSingleMatcher(_movieMatcher);

            var tracked = Subject.TrackDownload(Client(), Item());

            tracked.Kind.Should().Be(MediaKind.Movies);
            _movieMatcher.Verify(m => m.Match(tracked, null), Times.Once());
        }

        [Test]
        public void should_ignore_a_category_whose_media_type_is_not_installed()
        {
            GivenSingleMatcher(_movieMatcher);

            var tracked = Subject.TrackDownload(Client((MediaKind.Music, "music")), Item(category: "music"));

            tracked.Kind.Should().Be(MediaKind.Movies);
        }

        [Test]
        public void should_not_match_again_a_download_that_is_already_past_downloading()
        {
            GivenDownloadHistory(MediaKind.Movies);

            var first = Subject.TrackDownload(Client(), Item());
            first.State = TrackedDownloadState.Imported;

            var second = Subject.TrackDownload(Client(), Item());

            second.Should().BeSameAs(first);
            _movieMatcher.Verify(m => m.Match(It.IsAny<TrackedDownload>(), It.IsAny<DownloadHistory>()), Times.Once());
        }

        [Test]
        public void should_retrack_as_downloading_when_grabbed_again_after_being_imported()
        {
            GivenDownloadHistory(MediaKind.Movies, DownloadHistoryEventType.DownloadImported);

            var imported = Subject.TrackDownload(Client(), Item());
            imported.State.Should().Be(TrackedDownloadState.Imported);

            Subject.Handle(new ReleaseGrabbedEvent(new TvRemoteRelease()) { DownloadId = "35238" });

            GivenDownloadHistory(MediaKind.Movies);

            var regrabbed = Subject.TrackDownload(Client(), Item());
            regrabbed.State.Should().Be(TrackedDownloadState.Downloading);
        }

        [Test]
        public void rematch_should_ask_the_media_type_of_each_download_and_publish_the_refresh()
        {
            GivenDownloadHistory(MediaKind.Tv);

            var tv = Subject.TrackDownload(Client(), Item());
            var movie = Subject.TrackDownload(Client((MediaKind.Movies, "movies")), Item(category: "movies", downloadId: "999"));

            Subject.Rematch(new List<TrackedDownload> { tv });

            _tvMatcher.Verify(m => m.Rematch(tv), Times.Once());
            _movieMatcher.Verify(m => m.Rematch(It.IsAny<TrackedDownload>()), Times.Never());
            movie.Kind.Should().Be(MediaKind.Movies);
            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<TrackedDownloadRefreshedEvent>()), Times.Once());
        }

        [Test]
        public void rematch_should_do_nothing_for_an_empty_list()
        {
            Subject.Rematch(new List<TrackedDownload>());

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<TrackedDownloadRefreshedEvent>()), Times.Never());
        }
    }
}
