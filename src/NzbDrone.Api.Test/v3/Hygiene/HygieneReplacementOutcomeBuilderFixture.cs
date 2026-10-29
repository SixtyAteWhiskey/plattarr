using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Search;
using Radarr.Api.V3.Hygiene;

namespace NzbDrone.Api.Test.v3.Hygiene
{
    [TestFixture]
    public class HygieneReplacementOutcomeBuilderFixture
    {
        private static readonly DateTime Start = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void completed_search_with_no_grab_is_not_recovery()
        {
            var action = Action();
            action.ReplacementSearchCommandId = 8;
            action.ReplacementTargetIds = "1,2,3,4,5,6,7";
            action.ReplacementSearchStatus = "completed";
            action.ReplacementGrabCount = 0;
            var history = new Mock<IHistoryService>();
            var command = Command(8);
            var result = new HygieneReplacementOutcomeBuilder(history.Object, new[] { command }).Build(action);

            result.SearchState.Should().Be("completed-no-grab");
            result.ReportsGrabbed.Should().Be(0);
            result.TargetCount.Should().Be(7);
            result.Grabs.Should().BeEmpty();
        }

        [Test]
        public void partial_pack_covers_only_one_target_and_its_later_failure_is_not_an_import()
        {
            var action = Action();
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.Find("original", EpisodeHistoryEventType.DownloadFailed))
                .Returns(FailedOriginalRows());
            history.Setup(x => x.FindByEpisodeId(1)).Returns(new List<EpisodeHistory>
            {
                Row(1, "replacement", EpisodeHistoryEventType.Grabbed, 2),
                Row(1, "replacement", EpisodeHistoryEventType.DownloadFailed, 10)
            });
            history.Setup(x => x.FindByEpisodeId(2)).Returns(new List<EpisodeHistory>
            {
                Row(2, "replacement", EpisodeHistoryEventType.DownloadFolderImported, 10)
            });
            for (var id = 3; id <= 7; id++)
            {
                history.Setup(x => x.FindByEpisodeId(id)).Returns(new List<EpisodeHistory>());
            }

            var result = new HygieneReplacementOutcomeBuilder(history.Object, new[] { Command(8) }).Build(action);

            result.Evidence.Should().Be("inferred-command");
            result.TargetCount.Should().Be(7);
            result.Grabs.Should().ContainSingle().Which.Should().BeEquivalentTo(new HygieneReplacementGrab
            {
                DownloadId = "replacement",
                CoveredTargetCount = 1,
                ImportedTargetCount = 0,
                FailedTargetCount = 1
            });
        }

        [Test]
        public void import_is_reported_for_the_matching_download_and_episode_only()
        {
            var action = Action();
            action.ReplacementTargetIds = "1,2,3,4,5,6,7";
            action.ReplacementSearchCommandId = 8;
            action.ReplacementSearchStatus = "completed";
            action.ReplacementGrabCount = 1;
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.FindByEpisodeId(1)).Returns(new List<EpisodeHistory>
            {
                Row(1, "replacement", EpisodeHistoryEventType.Grabbed, 2),
                Row(1, "replacement", EpisodeHistoryEventType.DownloadFolderImported, 10)
            });
            for (var id = 2; id <= 7; id++)
            {
                history.Setup(x => x.FindByEpisodeId(id)).Returns(new List<EpisodeHistory>());
            }

            var result = new HygieneReplacementOutcomeBuilder(history.Object, new[] { Command(8) }).Build(action);

            result.SearchState.Should().Be("completed");
            result.Grabs.Should().ContainSingle().Which.ImportedTargetCount.Should().Be(1);
            result.Grabs[0].CoveredTargetCount.Should().Be(1);
        }

        [Test]
        public void old_action_without_a_unique_command_keeps_search_completion_unverified()
        {
            var action = Action();
            var history = new Mock<IHistoryService>();
            history.Setup(x => x.Find("original", EpisodeHistoryEventType.DownloadFailed))
                .Returns(FailedOriginalRows());

            var result = new HygieneReplacementOutcomeBuilder(history.Object, Array.Empty<CommandModel>()).Build(action);

            result.SearchState.Should().Be("requested");
            result.Evidence.Should().Be("unverified");
            result.TargetCount.Should().Be(7);
        }

        private static HygieneAction Action() => new()
        {
            Id = 11,
            MediaKind = (int)MediaKind.Tv,
            DownloadId = "original",
            Checkpoint = "replacement-requested",
            CreatedAt = Start,
            UpdatedAt = Start.AddMinutes(1)
        };

        private static CommandModel Command(int id) => new()
        {
            Id = id,
            Body = new EpisodeSearchCommand(new List<int> { 1, 2, 3, 4, 5, 6, 7 }),
            QueuedAt = Start.AddSeconds(1),
            StartedAt = Start.AddSeconds(2),
            EndedAt = Start.AddSeconds(5),
            Status = CommandStatus.Completed
        };

        private static List<EpisodeHistory> FailedOriginalRows()
        {
            var rows = new List<EpisodeHistory>();
            for (var id = 1; id <= 7; id++)
            {
                rows.Add(Row(id, "original", EpisodeHistoryEventType.DownloadFailed, 0));
            }

            return rows;
        }

        private static EpisodeHistory Row(int episodeId, string downloadId, EpisodeHistoryEventType type, int seconds) => new()
        {
            MediaKind = MediaKind.Tv,
            ParentId = 42,
            ItemId = episodeId,
            DownloadId = downloadId,
            EventType = type,
            Date = Start.AddSeconds(seconds)
        };
    }
}
