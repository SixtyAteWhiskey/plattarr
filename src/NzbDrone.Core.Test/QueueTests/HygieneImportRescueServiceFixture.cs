using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.MediaFiles.MovieImport.Manual;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using TvHistory = NzbDrone.Core.Tv.History.IHistoryService;
using TvManualImport = NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.IManualImportService;
using MovieManualImport = NzbDrone.Core.Movies.MediaFiles.MovieImport.Manual.IManualImportService;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneImportRescueServiceFixture
    {
        private Mock<IConfigService> _config;
        private Mock<IHygieneRuleService> _rules;
        private Mock<IHygieneImportRescueRepository> _rescues;
        private Mock<MovieManualImport> _movieImport;
        private Mock<TvManualImport> _tvImport;
        private Mock<IManageCommandQueue> _commands;
        private Mock<NzbDrone.Core.Movies.History.IHistoryService> _movieHistory;
        private Mock<TvHistory> _tvHistory;
        private Mock<IDownloadHistoryService> _grabHistory;
        private List<HygieneImportRescue> _records;
        private HygieneImportRescueService _service;
        private HygieneRule _rule;

        [SetUp]
        public void SetUp()
        {
            _config = new Mock<IConfigService>();
            _rules = new Mock<IHygieneRuleService>();
            _rescues = new Mock<IHygieneImportRescueRepository>();
            _movieImport = new Mock<MovieManualImport>();
            _tvImport = new Mock<TvManualImport>();
            _commands = new Mock<IManageCommandQueue>();
            _movieHistory = new Mock<NzbDrone.Core.Movies.History.IHistoryService>();
            _tvHistory = new Mock<TvHistory>();
            _grabHistory = new Mock<IDownloadHistoryService>();
            _records = new List<HygieneImportRescue>();
            _rule = new HygieneRule { Id = 7, ReasonCode = "import-blocked", Enabled = true,
                ForceImportEnabled = true, ForceImportMaxTries = 3, StrikeLimit = 3, StrikeIntervalMinutes = 5 };
            _config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            _rules.Setup(x => x.All()).Returns(new List<HygieneRule> { _rule });
            _rules.Setup(x => x.Match(It.IsAny<IEnumerable<HygieneRule>>(), "import-blocked", 1, 4)).Returns(_rule);
            _rescues.Setup(x => x.FindLatest(4, "hash")).Returns(() => _records.LastOrDefault());
            _rescues.Setup(x => x.FindPending()).Returns(() => _records.Where(x => x.Status == "requested").ToList());
            _rescues.Setup(x => x.Insert(It.IsAny<HygieneImportRescue>())).Returns((HygieneImportRescue rescue) =>
            {
                rescue.Id = _records.Count + 1;
                _records.Add(rescue);
                return rescue;
            });
            _commands.Setup(x => x.GetStarted()).Returns(new List<CommandModel>());
            _commands.Setup(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                .Returns(new CommandModel { Id = 99, QueuedAt = DateTime.UtcNow.AddMinutes(-1), Status = CommandStatus.Queued });
            _commands.Setup(x => x.Get(99)).Returns(new CommandModel { Id = 99, Status = CommandStatus.Queued });
            _movieHistory.Setup(x => x.FindByDownloadId(It.IsAny<string>())).Returns(new List<MovieHistory>());
            _tvHistory.Setup(x => x.FindByDownloadId(It.IsAny<string>()))
                .Returns(new List<NzbDrone.Core.Tv.History.EpisodeHistory>());
            _movieImport.Setup(x => x.GetMediaFiles(null, "hash", 5, false)).Returns(new List<ManualImportItem>
            {
                new() { Path = "/fixture/movie.mkv", Movie = new Movie { Id = 5 } }
            });
            _service = new HygieneImportRescueService(_config.Object, _rules.Object, _rescues.Object,
                _movieImport.Object, _tvImport.Object, _commands.Object, _movieHistory.Object,
                _tvHistory.Object, _grabHistory.Object, NLog.LogManager.GetCurrentClassLogger());
        }

        [Test]
        public void eligible_movie_requests_one_import_and_holds_strikes()
        {
            var item = Item();

            _service.Process(new[] { item });
            item.HygieneImportRescueStatus.Should().Be("settling");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Never);
            _service.Process(new[] { item });

            item.HygieneImportRescueStatus.Should().Be("requested");
            _records.Should().ContainSingle();
            _records[0].Attempts.Should().Be(1);
            _records[0].CommandId.Should().Be(99);
            _commands.Verify(x => x.Push(It.Is<ManualImportCommand>(command =>
                command.Files.Count == 1 && command.Files[0].MovieId == 5 && command.Files[0].DownloadId == "hash"),
                It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void changed_candidate_file_set_must_settle_again()
        {
            var item = Item();
            _service.Process(new[] { item });
            var firstFingerprint = _records[0].CandidateFingerprint;
            _movieImport.Setup(x => x.GetMediaFiles(null, "hash", 5, false)).Returns(new List<ManualImportItem>
            {
                new() { Path = "/fixture/replaced.mkv", Movie = new Movie { Id = 5 } }
            });

            _service.Process(new[] { item });

            _records[0].CandidateFingerprint.Should().NotBe(firstFingerprint);
            item.HygieneImportRescueStatus.Should().Be("settling");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Never);
            _service.Process(new[] { item });
            item.HygieneImportRescueStatus.Should().Be("requested");
        }

        [Test]
        public void unsafe_intervening_probe_resets_settling()
        {
            var item = Item();
            _service.Process(new[] { item });
            var safe = _movieImport.Object.GetMediaFiles(null, "hash", 5, false);
            _movieImport.Setup(x => x.GetMediaFiles(null, "hash", 5, false))
                .Returns(new List<ManualImportItem>());
            _service.Process(new[] { item });
            item.HygieneImportRescueStatus.Should().BeNull();
            _movieImport.Setup(x => x.GetMediaFiles(null, "hash", 5, false)).Returns(safe);

            _service.Process(new[] { item });

            item.HygieneImportRescueStatus.Should().Be("settling");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Never);
        }

        [Test]
        public void unrelated_rejection_returns_to_strike_path_without_importing()
        {
            _movieImport.Setup(x => x.GetMediaFiles(null, "hash", 5, false)).Returns(new List<ManualImportItem>
            {
                new() { Path = "/fixture/movie.mkv", Movie = new Movie { Id = 5 },
                    Rejections = new[] { new NzbDrone.Core.Movies.MediaFiles.MovieImport.ImportRejection(
                        NzbDrone.Core.Movies.MediaFiles.MovieImport.ImportRejectionReason.MinimumFreeSpace, "Not enough free space") } }
            });
            var item = Item();

            _service.Process(new[] { item });

            item.HygieneImportRescueStatus.Should().BeNull();
            _records.Should().BeEmpty();
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Never);
        }

        [Test]
        public void private_torrent_does_not_force_import()
        {
            var item = Item();
            item.Protocol = DownloadProtocol.Torrent;
            item.RemoteRelease.Release.IndexerPrivacy = IndexerPrivacy.Private;

            _service.Process(new[] { item });

            _records.Should().BeEmpty();
            item.HygieneImportRescueStatus.Should().BeNull();
        }

        [Test]
        public void later_import_history_confirms_requested_command()
        {
            var item = Item();
            _service.Process(new[] { item });
            _service.Process(new[] { item });
            _movieHistory.Setup(x => x.FindByDownloadId("hash")).Returns(new List<MovieHistory>
            {
                new() { ParentId = 5, DownloadId = "hash", EventType = MovieHistoryEventType.DownloadFolderImported,
                    Date = DateTime.UtcNow }
            });

            _service.Process(new[] { item });

            _records[0].Status.Should().Be("confirmed");
            item.HygieneImportRescueStatus.Should().Be("confirmed");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void command_error_waits_before_retrying()
        {
            var item = Item();
            _service.Process(new[] { item });
            _service.Process(new[] { item });
            _commands.Setup(x => x.Get(99)).Returns(new CommandModel { Id = 99, Status = CommandStatus.Failed });

            _service.Process(new[] { item });

            _records[0].Status.Should().Be("failed");
            _records[0].NextAttemptAt.Should().BeAfter(DateTime.UtcNow);
            item.HygieneImportRescueStatus.Should().Be("failed");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void running_import_holds_without_issuing_another_command()
        {
            _commands.Setup(x => x.GetStarted()).Returns(new List<CommandModel>
            {
                new() { Name = "DownloadedMoviesScan", Status = CommandStatus.Started }
            });
            var item = Item();

            _service.Process(new[] { item });

            item.HygieneImportRescueStatus.Should().Be("waiting-for-imports");
            _records.Should().BeEmpty();
        }

        [Test]
        public void ambiguous_command_request_is_not_retried_blindly()
        {
            _commands.Setup(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                .Throws(new InvalidOperationException());
            var item = Item();

            _service.Process(new[] { item });
            _service.Process(new[] { item });
            _service.Process(new[] { item });

            _records.Should().ContainSingle().Which.Status.Should().Be("ambiguous");
            item.HygieneImportRescueStatus.Should().Be("ambiguous");
            _commands.Verify(x => x.Push(It.IsAny<ManualImportCommand>(), It.IsAny<CommandPriority>(),
                It.IsAny<CommandTrigger>()), Times.Once);
        }

        [Test]
        public void import_history_before_request_does_not_confirm_rescue()
        {
            var item = Item();
            _service.Process(new[] { item });
            _service.Process(new[] { item });
            var requestedAt = _records[0].RequestedAt.Value;
            _movieHistory.Setup(x => x.FindByDownloadId("hash")).Returns(new List<MovieHistory>
            {
                new() { ParentId = 5, EventType = MovieHistoryEventType.DownloadFolderImported,
                    Date = requestedAt.AddSeconds(-1) }
            });

            _service.Process(new[] { item });

            _records[0].Status.Should().Be("requested");
        }

        [Test]
        public void matching_tv_file_maps_episodes_before_import_request()
        {
            var item = new QueueItem
            {
                Id = 12,
                DownloadClientId = 4,
                DownloadId = "tv-hash",
                Protocol = DownloadProtocol.Usenet,
                TrackedDownloadState = TrackedDownloadState.ImportBlocked,
                RemoteRelease = new RemoteEpisode
                {
                    Series = new Series { Id = 8 },
                    Episodes = new List<Episode> { new() { Id = 20 } }
                },
                StatusMessages = new List<TrackedDownloadStatusMessage>
                {
                    new("tv", "Found matching series via grab history, but release was matched to series by ID")
                }
            };
            _rules.Setup(x => x.Match(It.IsAny<IEnumerable<HygieneRule>>(), "import-blocked", 2, 4)).Returns(_rule);
            _tvImport.Setup(x => x.GetMediaFiles(null, "tv-hash", 8, false))
                .Returns(new List<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.ManualImportItem>
                {
                    new() { Path = "/fixture/episode.mkv", Series = new Series { Id = 8 },
                        Episodes = new List<Episode> { new() { Id = 20 } } }
                });
            _commands.Setup(x => x.Push(It.IsAny<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.TvManualImportCommand>(),
                    It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()))
                .Returns(new CommandModel { Id = 100, QueuedAt = DateTime.UtcNow, Status = CommandStatus.Queued });
            _rescues.Setup(x => x.FindLatest(4, "tv-hash")).Returns(() => _records.LastOrDefault());

            _service.Process(new[] { item });
            item.HygieneImportRescueStatus.Should().Be("settling");
            _service.Process(new[] { item });

            item.HygieneImportRescueStatus.Should().Be("requested");
            _records.Should().ContainSingle().Which.TargetIds.Should().Be("20");
            _commands.Verify(x => x.Push(
                It.Is<NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Manual.TvManualImportCommand>(command =>
                    command.Files.Count == 1 && command.Files[0].EpisodeIds.Single() == 20),
                It.IsAny<CommandPriority>(), It.IsAny<CommandTrigger>()), Times.Once);
        }

        private static QueueItem Item()
        {
            return new QueueItem
            {
                Id = 11,
                DownloadClientId = 4,
                DownloadId = "hash",
                Protocol = DownloadProtocol.Usenet,
                TrackedDownloadState = TrackedDownloadState.ImportBlocked,
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 5 },
                    Release = new ReleaseInfo { IndexerPrivacy = IndexerPrivacy.Public } },
                StatusMessages = new List<TrackedDownloadStatusMessage>
                {
                    new("movie", "Found matching movie via grab history, but release was matched to movie by ID")
                }
            };
        }
    }
}
