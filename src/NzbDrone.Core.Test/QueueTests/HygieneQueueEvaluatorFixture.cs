using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Queue;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueEvaluatorFixture
    {
        private readonly HygieneQueueEvaluator _evaluator = new();

        [Test]
        public void import_blocked_is_explained_without_proposing_an_automatic_action()
        {
            var queue = new QueueItem
            {
                Id = 42,
                Title = "Test release",
                DownloadClient = "Test client",
                RemoteRelease = new RemoteMovie(),
                Status = QueueStatus.Completed,
                TrackedDownloadState = TrackedDownloadState.ImportBlocked,
                TrackedDownloadStatus = TrackedDownloadStatus.Warning
            };

            var result = _evaluator.Evaluate(new[] { queue }).Should().ContainSingle().Subject;

            result.QueueId.Should().Be(42);
            result.Kind.Should().Be(MediaKind.Movies);
            result.ReasonCode.Should().Be("import-blocked");
            result.NeedsAttention.Should().BeTrue();
            result.WouldAct.Should().BeFalse();
        }

        [Test]
        public void ignored_download_is_never_classified_as_a_failure()
        {
            var queue = new QueueItem
            {
                Status = QueueStatus.Failed,
                TrackedDownloadState = TrackedDownloadState.Ignored,
                TrackedDownloadStatus = TrackedDownloadStatus.Error
            };

            var result = _evaluator.Evaluate(new[] { queue }).Should().ContainSingle().Subject;

            result.ReasonCode.Should().Be("ignored");
            result.NeedsAttention.Should().BeFalse();
            result.WouldAct.Should().BeFalse();
        }

        [Test]
        public void evaluator_keeps_client_failure_and_healthy_items_distinct()
        {
            var queue = new List<QueueItem>
            {
                new() { Id = 1, Status = QueueStatus.Failed },
                new() { Id = 2, Status = QueueStatus.Downloading }
            };

            var results = _evaluator.Evaluate(queue);

            results[0].ReasonCode.Should().Be("client-failed");
            results[0].NeedsAttention.Should().BeTrue();
            results[1].ReasonCode.Should().Be("healthy");
            results[1].NeedsAttention.Should().BeFalse();
            results.Should().OnlyContain(x => !x.WouldAct);
        }

        [Test]
        public void unavailable_client_has_a_specific_reason_even_if_a_warning_is_present()
        {
            var queue = new QueueItem
            {
                Status = QueueStatus.DownloadClientUnavailable,
                TrackedDownloadStatus = TrackedDownloadStatus.Warning
            };

            var result = _evaluator.Evaluate(new[] { queue }).Should().ContainSingle().Subject;

            result.ReasonCode.Should().Be("client-unavailable");
            result.NeedsAttention.Should().BeTrue();
        }
    }
}
