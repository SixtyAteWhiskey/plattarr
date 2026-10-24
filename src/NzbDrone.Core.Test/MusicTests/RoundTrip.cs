using System;
using FluentAssertions;
using FluentAssertions.Equivalency;

namespace NzbDrone.Core.Test.MusicTests
{
    internal static class RoundTrip
    {
        // Dates come back as UTC from either database; compare them to the second.
        public static EquivalencyAssertionOptions<T> WithDateTolerance<T>(this EquivalencyAssertionOptions<T> options)
        {
            return options
                .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation.ToUniversalTime(), TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime>()
                .Using<DateTime?>(c => c.Subject.Should().BeCloseTo(c.Expectation.Value.ToUniversalTime(), TimeSpan.FromSeconds(1))).WhenTypeIs<DateTime?>();
        }
    }
}
