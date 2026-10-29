using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneUnstableStrikeCalculatorFixture
    {
        private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void alternating_signals_count_once_per_interval_and_recovery_preserves_the_total()
        {
            var state = new HygieneQueueState();

            HygieneUnstableStrikeCalculator.Advance(state, "download-slow", 8, Now, 5);
            HygieneUnstableStrikeCalculator.Advance(state, "download-stalled", 8, Now.AddMinutes(1), 5);
            state.UnstableStrikes.Should().Be(1);

            HygieneUnstableStrikeCalculator.Advance(state, "download-stalled", 8, Now.AddMinutes(5), 5);
            HygieneUnstableStrikeCalculator.Advance(state, "healthy", 8, Now.AddMinutes(6), 5);
            HygieneUnstableStrikeCalculator.Advance(state, "download-slow", 8, Now.AddMinutes(7), 5);
            state.UnstableStrikes.Should().Be(2);

            HygieneUnstableStrikeCalculator.Advance(state, "download-slow", 8, Now.AddMinutes(10), 5);
            state.UnstableStrikes.Should().Be(3);
        }

        [Test]
        public void disabling_the_rule_clears_prior_cumulative_strikes()
        {
            var state = new HygieneQueueState();
            HygieneUnstableStrikeCalculator.Advance(state, "download-stalled", 8, Now, 5);
            HygieneUnstableStrikeCalculator.Advance(state, "healthy", 0, Now.AddMinutes(1), 5);

            state.UnstableStrikes.Should().Be(0);
            state.UnstableRuleId.Should().Be(0);
            state.LastUnstableStrikeAt.Should().BeNull();
        }

        [Test]
        public void changing_the_scoped_rule_starts_a_new_count()
        {
            var state = new HygieneQueueState();
            HygieneUnstableStrikeCalculator.Advance(state, "download-slow", 8, Now, 5);
            HygieneUnstableStrikeCalculator.Advance(state, "download-stalled", 9, Now.AddMinutes(1), 5);

            state.UnstableStrikes.Should().Be(1);
            state.UnstableRuleId.Should().Be(9);
        }
    }
}
