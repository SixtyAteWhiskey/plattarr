using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Queue;
using QueueItem = NzbDrone.Core.Queue.Queue;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneQueueStrikeServiceFixture
    {
        [Test]
        public void should_not_accumulate_strikes_until_a_matching_rule_is_enabled()
        {
            var savedStates = new List<HygieneQueueState>();
            var savedRules = new List<HygieneRule>();
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.FindByIdentities(It.IsAny<IEnumerable<(int, string)>>()))
                .Returns((IEnumerable<(int, string)> identities) =>
                    savedStates.Where(state => identities.Contains((state.DownloadClientId, state.DownloadId))).ToList());
            states.Setup(x => x.DeleteExpired(It.IsAny<DateTime>()))
                .Callback((DateTime cutoff) => savedStates.RemoveAll(state => state.LastSeenAt < cutoff));
            states.Setup(x => x.Insert(It.IsAny<HygieneQueueState>()))
                .Returns((HygieneQueueState state) =>
                {
                    savedStates.Add(state);
                    return state;
                });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(() => savedRules);
            var service = new HygieneQueueStrikeService(states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), new HygieneQueueEvaluator());
            var item = new QueueItem
            {
                DownloadClientId = 4,
                DownloadId = "isolated-test",
                Status = QueueStatus.Failed,
                SizeLeft = 100
            };

            service.Observe(new[] { item });

            savedStates.Should().ContainSingle().Which.Strikes.Should().Be(0);

            savedRules.Add(new HygieneRule
            {
                ReasonCode = "client-failed",
                Enabled = true,
                StrikeLimit = 3,
                StrikeIntervalMinutes = 5
            });

            service.Observe(new[] { item });

            savedStates.Should().ContainSingle().Which.Strikes.Should().Be(1);
        }

        [Test]
        public void should_refresh_queued_state_and_retire_only_expired_absent_states()
        {
            var savedStates = new List<HygieneQueueState>
            {
                new() { Id = 1, DownloadClientId = 4, DownloadId = "queued", LastSeenAt = DateTime.UtcNow.AddDays(-31) },
                new() { Id = 2, DownloadClientId = 4, DownloadId = "expired", LastSeenAt = DateTime.UtcNow.AddDays(-31) },
                new() { Id = 3, DownloadClientId = 4, DownloadId = "recent", LastSeenAt = DateTime.UtcNow.AddDays(-1) }
            };
            var states = new Mock<IHygieneQueueStateRepository>();
            states.Setup(x => x.FindByIdentities(It.IsAny<IEnumerable<(int, string)>>()))
                .Returns((IEnumerable<(int, string)> identities) =>
                    savedStates.Where(state => identities.Contains((state.DownloadClientId, state.DownloadId))).ToList());
            states.Setup(x => x.DeleteExpired(It.IsAny<DateTime>()))
                .Callback((DateTime cutoff) => savedStates.RemoveAll(state => state.LastSeenAt < cutoff));
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(new List<HygieneRule>());
            var service = new HygieneQueueStrikeService(states.Object, new HygieneRuleService(rules.Object, NLog.LogManager.GetCurrentClassLogger()), new HygieneQueueEvaluator());

            service.Observe(new[] { new QueueItem { DownloadClientId = 4, DownloadId = "queued", Status = QueueStatus.Failed, SizeLeft = 100 } });

            savedStates.Select(state => state.DownloadId).Should().BeEquivalentTo(new[] { "queued", "recent" });
            savedStates.Single(state => state.DownloadId == "queued").LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            states.Verify(x => x.All(), Times.Never);
            states.Verify(x => x.DeleteExpired(It.IsAny<DateTime>()), Times.Once);
        }
    }
}
