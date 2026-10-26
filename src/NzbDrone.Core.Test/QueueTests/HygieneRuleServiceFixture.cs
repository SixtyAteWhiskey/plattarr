using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneRuleServiceFixture : CoreTest<HygieneRuleService>
    {
        [Test]
        public void more_specific_disabled_rule_overrides_a_global_rule()
        {
            var rules = new[]
            {
                new HygieneRule { Id = 1, ReasonCode = "import-blocked", Enabled = true },
                new HygieneRule { Id = 2, ReasonCode = "import-blocked", MediaKind = 2, Enabled = false },
                new HygieneRule { Id = 3, ReasonCode = "import-blocked", MediaKind = 2, DownloadClientId = 4, Enabled = true }
            };

            Subject.Match(rules, "import-blocked", 2, 4).Id.Should().Be(3);
            Subject.Match(rules, "import-blocked", 2, 5).Id.Should().Be(2);
            Subject.Match(rules, "import-blocked", 1, 5).Id.Should().Be(1);
        }

        [Test]
        public void invalid_threshold_is_rejected()
        {
            var rule = new HygieneRule
            {
                ReasonCode = "import-blocked",
                StrikeLimit = 0,
                StrikeIntervalMinutes = 5
            };

            Action save = () => Subject.Upsert(rule);

            save.Should().Throw<ArgumentException>();
        }
    }
}
