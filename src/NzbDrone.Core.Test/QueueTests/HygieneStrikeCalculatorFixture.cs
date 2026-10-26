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
        public void progress_should_reset_consecutive_strikes()
        {
            var state = HygieneStrikeCalculator.Advance(null, "download-warning", true, 100, Now, 5);
            HygieneStrikeCalculator.Advance(state, "download-warning", true, 100, Now.AddMinutes(5), 5);

            HygieneStrikeCalculator.Advance(state, "download-warning", true, 90, Now.AddMinutes(6), 5).Strikes.Should().Be(0);
            HygieneStrikeCalculator.Advance(state, "download-warning", true, 90, Now.AddMinutes(7), 5).Strikes.Should().Be(1);
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
