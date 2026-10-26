using System.Collections.Generic;
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
            states.Setup(x => x.All()).Returns(() => savedStates);
            states.Setup(x => x.Insert(It.IsAny<HygieneQueueState>()))
                .Returns((HygieneQueueState state) =>
                {
                    savedStates.Add(state);
                    return state;
                });
            var rules = new Mock<IHygieneRuleRepository>();
            rules.Setup(x => x.All()).Returns(() => savedRules);
            var service = new HygieneQueueStrikeService(states.Object, new HygieneRuleService(rules.Object), new HygieneQueueEvaluator());
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
    }
}
