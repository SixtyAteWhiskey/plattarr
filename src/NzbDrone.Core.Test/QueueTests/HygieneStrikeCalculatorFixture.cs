using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneStrikeCalculatorFixture
    {
        private static readonly DateTime Now = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void should_count_only_after_the_configured_interval()
        {
            var state = HygieneStrikeCalculator.Advance(null, "import-blocked", true, 100, Now, 5);

            state.Strikes.Should().Be(1);
            HygieneStrikeCalculator.Advance(state, "import-blocked", true, 100, Now.AddMinutes(4), 5).Strikes.Should().Be(1);
            HygieneStrikeCalculator.Advance(state, "import-blocked", true, 100, Now.AddMinutes(5), 5).Strikes.Should().Be(2);
        }

        [Test]
        public void metadata_strikes_count_at_intervals_and_reset_after_metadata_completes()
        {
            var state = HygieneStrikeCalculator.Advance(null, "download-metadata", true, 0, Now, 5);
            HygieneStrikeCalculator.Advance(state, "download-metadata", true, 0, Now.AddMinutes(4), 5).Strikes.Should().Be(1);
            HygieneStrikeCalculator.Advance(state, "download-metadata", true, 0, Now.AddMinutes(5), 5).Strikes.Should().Be(2);
            HygieneStrikeCalculator.Advance(state, "healthy", false, 100, Now.AddMinutes(6), 5).Strikes.Should().Be(0);
            HygieneStrikeCalculator.Advance(state, "download-metadata", true, 0, Now.AddMinutes(7), 5).Strikes.Should().Be(1);
        }

        [Test]
        public void progress_should_reset_consecutive_strikes()
        {
            var state = HygieneStrikeCalculator.Advance(null, "download-warning", true, 100, Now, 5);
            HygieneStrikeCalculator.Advance(state, "download-warning", true, 100, Now.AddMinutes(5), 5);

            HygieneStrikeCalculator.Advance(state, "download-warning", true, 90, Now.AddMinutes(6), 5).Strikes.Should().Be(0);
            HygieneStrikeCalculator.Advance(state, "download-warning", true, 90, Now.AddMinutes(7), 5).Strikes.Should().Be(1);
        }

        [Test]
        public void stalled_progress_can_wait_for_a_configured_cumulative_amount()
        {
            var state = HygieneStrikeCalculator.Advance(null, "download-stalled", true, 100, Now, 5, true, 10);
            HygieneStrikeCalculator.Advance(state, "download-stalled", true, 96, Now.AddMinutes(5), 5, true, 10).Strikes.Should().Be(2);
            HygieneStrikeCalculator.Advance(state, "download-stalled", true, 92, Now.AddMinutes(6), 5, true, 10).Strikes.Should().Be(2);
            HygieneStrikeCalculator.Advance(state, "download-stalled", true, 90, Now.AddMinutes(7), 5, true, 10).Strikes.Should().Be(0);
            state.ProgressBaselineRemainingSize.Should().Be(90);
        }

        [Test]
        public void progress_reset_can_be_disabled_for_stalled_and_enabled_for_slow()
        {
            var stalled = HygieneStrikeCalculator.Advance(null, "download-stalled", true, 100, Now, 5, false);
            HygieneStrikeCalculator.Advance(stalled, "download-stalled", true, 90, Now.AddMinutes(5), 5, false).Strikes.Should().Be(2);

            var slow = HygieneStrikeCalculator.Advance(null, "download-slow", true, 100, Now, 5, true);
            HygieneStrikeCalculator.Advance(slow, "download-slow", true, 99, Now.AddMinutes(5), 5, true).Strikes.Should().Be(0);
        }

        [Test]
        public void switching_privacy_rules_starts_a_new_consecutive_count()
        {
            var state = HygieneStrikeCalculator.Advance(null, "download-stalled", true, 100, Now, 5, true, 0, 1);
            HygieneStrikeCalculator.Advance(state, "download-stalled", true, 100, Now.AddMinutes(5), 5, true, 0, 1).Strikes.Should().Be(2);
            HygieneStrikeCalculator.Advance(state, "download-stalled", true, 100, Now.AddMinutes(6), 5, true, 0, 2).Strikes.Should().Be(1);
            state.ConsecutiveRuleId.Should().Be(2);
        }

        [Test]
        public void changing_reason_or_recovering_should_reset_the_count()
        {
            var state = HygieneStrikeCalculator.Advance(null, "client-failed", true, 0, Now, 5);
            HygieneStrikeCalculator.Advance(state, "import-blocked", true, 0, Now.AddMinutes(5), 5).Strikes.Should().Be(1);
            HygieneStrikeCalculator.Advance(state, "healthy", false, 0, Now.AddMinutes(6), 5).Strikes.Should().Be(0);
        }
    }
}
