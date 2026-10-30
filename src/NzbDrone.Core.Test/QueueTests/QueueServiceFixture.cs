using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class QueueServiceFixture : CoreTest<QueueService>
    {
        private List<TrackedDownload> _trackedDownloads;
        private RemoteMovie _remoteMovie;
        private Movie _movie;

        [SetUp]
        public void SetUp()
        {
            var downloadClientInfo = Builder<DownloadClientItemClientInfo>.CreateNew().Build();

            var downloadItem = Builder<NzbDrone.Core.Download.DownloadClientItem>.CreateNew()
                                        .With(v => v.RemainingTime = TimeSpan.FromSeconds(10))
                                        .With(v => v.DownloadClientInfo = downloadClientInfo)
                                        .Build();

            _movie = Builder<Movie>.CreateNew()
                                   .Build();

            _remoteMovie = Builder<RemoteMovie>.CreateNew()
                                               .With(r => r.Movie = _movie)
                                               .With(r => r.ParsedMovieInfo = new ParsedMovieInfo { Quality = new QualityModel(Quality.Bluray1080p) })
                                               .Build();

            _trackedDownloads = Builder<TrackedDownload>.CreateListOfSize(1)
                .All()
                .With(v => v.IsTrackable = true)
                .With(v => v.DownloadItem = downloadItem)
                .With(v => v.RemoteRelease = _remoteMovie)
                .Build()
                .ToList();
        }

        [Test]
        public void queue_items_should_have_id()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();

            queue.Should().HaveCount(1);

            queue.All(v => v.Id > 0).Should().BeTrue();

            var distinct = queue.Select(v => v.Id).Distinct().ToArray();

            distinct.Should().HaveCount(1);
        }

        [Test]
        public void should_carry_the_movie_match_as_a_release()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeSameAs(_remoteMovie);
            queue.GetRemoteMovie().Should().BeSameAs(_remoteMovie);
            queue.GetMovie().Should().BeSameAs(_movie);
            queue.Quality.Quality.Should().Be(Quality.Bluray1080p);
            queue.Languages.Should().BeSameAs(_remoteMovie.Languages);
        }

        [Test]
        public void should_map_a_download_matched_by_another_module_without_any_movie()
        {
            var language = new List<Language> { Language.French };
            var release = new TvRemoteRelease
            {
                Languages = language,
                ParsedQuality = new QualityModel(Quality.HDTV720p)
            };

            _trackedDownloads[0].RemoteRelease = release;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeSameAs(release);
            queue.Quality.Quality.Should().Be(Quality.HDTV720p);
            queue.Languages.Should().BeSameAs(language);
            queue.GetRemoteMovie().Should().BeNull();
            queue.GetMovie().Should().BeNull();
        }

        [Test]
        public void should_use_unknown_quality_and_language_for_an_unmatched_download()
        {
            _trackedDownloads[0].RemoteRelease = null;
            _trackedDownloads[0].Kind = MediaKind.Music;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeNull();
            queue.Kind.Should().Be(MediaKind.Music);
            queue.Quality.Quality.Should().Be(Quality.Unknown);
            queue.Languages.Should().BeEquivalentTo(new[] { Language.Unknown });
            queue.GetMovie().Should().BeNull();
        }

        [Test]
        public void import_pending_refresh_does_not_reset_blocked_strikes_before_processing_finishes()
        {
            var observed = new List<TrackedDownloadState?>();
            Mocker.GetMock<IHygieneQueueStrikeService>()
                .Setup(x => x.Observe(It.IsAny<IEnumerable<NzbDrone.Core.Queue.Queue>>()))
                .Callback<IEnumerable<NzbDrone.Core.Queue.Queue>>(items => observed.AddRange(items.Select(x => x.TrackedDownloadState)));

            _trackedDownloads[0].State = TrackedDownloadState.ImportBlocked;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads, processingComplete: true));
            _trackedDownloads[0].State = TrackedDownloadState.ImportPending;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));
            _trackedDownloads[0].State = TrackedDownloadState.ImportBlocked;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads, processingComplete: true));
            _trackedDownloads[0].State = TrackedDownloadState.ImportPending;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads, processingComplete: true));

            observed.Should().Equal(TrackedDownloadState.ImportBlocked, TrackedDownloadState.ImportBlocked, TrackedDownloadState.ImportPending);
        }

        [Test]
        public void blocked_strikes_survive_intermediate_pending_and_reset_after_processed_recovery()
        {
            var tracked = _trackedDownloads[0];
            tracked.DownloadClient = 7;
            tracked.DownloadItem.DownloadId = "synthetic-import";
            tracked.DownloadItem.Status = DownloadItemStatus.Completed;
            tracked.State = TrackedDownloadState.ImportBlocked;
            var state = new HygieneQueueState
            {
                DownloadClientId = 7,
                DownloadId = "synthetic-import",
                ReasonCode = "import-blocked",
                Strikes = 1,
                RemainingSize = tracked.DownloadItem.RemainingSize,
                LastSeenAt = DateTime.UtcNow.AddMinutes(-3),
                LastStrikeAt = DateTime.UtcNow.AddMinutes(-3)
            };
            var stateRepository = new Mock<IHygieneQueueStateRepository>();
            stateRepository.Setup(x => x.FindByIdentities(It.IsAny<IEnumerable<(int, string)>>())).Returns(new[] { state });
            var ruleRepository = new Mock<IHygieneRuleRepository>();
            ruleRepository.Setup(x => x.All()).Returns(new List<HygieneRule>
            {
                new() { Id = 3, ReasonCode = "import-blocked", Enabled = true, StrikeLimit = 3, StrikeIntervalMinutes = 2 }
            });
            var strikes = new HygieneQueueStrikeService(stateRepository.Object, new HygieneRuleService(ruleRepository.Object, NLog.LogManager.GetCurrentClassLogger()), new HygieneQueueEvaluator());
            Mocker.SetConstant<IHygieneQueueStrikeService>(strikes);

            tracked.State = TrackedDownloadState.ImportPending;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));
            state.Strikes.Should().Be(1);
            tracked.State = TrackedDownloadState.ImportBlocked;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads, processingComplete: true));
            state.Strikes.Should().Be(2);
            tracked.State = TrackedDownloadState.ImportPending;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads, processingComplete: true));
            state.Strikes.Should().Be(0);
        }

        [Test]
        public void queue_carries_metadata_state_from_client()
        {
            _trackedDownloads[0].DownloadItem.DownloadClientDownloadingMetadata = true;
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));
            Subject.GetQueue().Single().DownloadClientDownloadingMetadata.Should().BeTrue();
        }

        [Test]
        public void strike_storage_failure_should_not_prevent_the_queue_from_refreshing()
        {
            Mocker.GetMock<IHygieneQueueStrikeService>()
                .Setup(x => x.Observe(It.IsAny<IEnumerable<NzbDrone.Core.Queue.Queue>>()))
                .Throws(new InvalidOperationException("test storage failure"));

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            Subject.GetQueue().Should().HaveCount(1);
        }
    }
}
