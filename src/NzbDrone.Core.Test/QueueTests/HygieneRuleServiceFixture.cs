using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NLog;
using NLog.Config;
using NLog.Targets;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneRuleServiceFixture : CoreTest<HygieneRuleService>
    {
        [Test]
        public void cumulative_rule_accepts_a_twelve_strike_threshold()
        {
            var rule = new HygieneRule
            {
                ReasonCode = "download-unstable",
                StrikeLimit = 12,
                StrikeIntervalMinutes = 5
            };
            var repository = Mocker.GetMock<IHygieneRuleRepository>();
            repository.Setup(x => x.All()).Returns(new List<HygieneRule>());
            repository.Setup(x => x.Insert(rule)).Returns(rule);

            Subject.Upsert(rule).Should().BeSameAs(rule);
        }

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

        [Test]
        public void strike_threshold_below_three_is_rejected()
        {
            var rule = new HygieneRule
            {
                ReasonCode = "import-blocked",
                StrikeLimit = 2,
                StrikeIntervalMinutes = 5
            };

            Action save = () => Subject.Upsert(rule);

            save.Should().Throw<ArgumentException>();
        }

        [Test]
        public void monitor_only_rule_cannot_request_source_file_deletion()
        {
            var rule = new HygieneRule
            {
                ReasonCode = "download-warning",
                DeleteFilesOnFailure = true,
                StrikeLimit = 3,
                StrikeIntervalMinutes = 5
            };

            Action save = () => Subject.Upsert(rule);

            save.Should().Throw<ArgumentException>();
        }

        [Test]
        public void enabled_state_change_is_written_to_the_application_log()
        {
            const string targetName = "HygieneRuleServiceFixture";
            var target = new MemoryTarget { Layout = "${message}" };
            var loggingConfiguration = LogManager.Configuration;
            var loggingRule = new LoggingRule("*", LogLevel.Info, target);
            loggingConfiguration.AddTarget(targetName, target);
            loggingConfiguration.LoggingRules.Add(loggingRule);
            LogManager.ReconfigExistingLoggers();

            try
            {
                var existingRule = new HygieneRule
                {
                    Id = 8,
                    ReasonCode = "download-stalled",
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                };
                var savedRule = existingRule;
                var repository = Mocker.GetMock<IHygieneRuleRepository>();
                repository.Setup(x => x.Find(existingRule.Id)).Returns(() => savedRule);
                repository.Setup(x => x.All()).Returns(() => new List<HygieneRule> { savedRule });
                repository.Setup(x => x.Update(It.IsAny<HygieneRule>())).Returns<HygieneRule>(rule => rule);

                var disabledRule = new HygieneRule
                {
                    Id = existingRule.Id,
                    ReasonCode = existingRule.ReasonCode,
                    Enabled = false,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                };
                Subject.Upsert(disabledRule);
                savedRule = disabledRule;

                var enabledRule = new HygieneRule
                {
                    Id = existingRule.Id,
                    ReasonCode = existingRule.ReasonCode,
                    Enabled = true,
                    StrikeLimit = 3,
                    StrikeIntervalMinutes = 5
                };
                Subject.Upsert(enabledRule);

                target.Logs.Should().HaveCount(2);
                target.Logs[0].Should().Contain("monitoring changed from enabled to disabled");
                target.Logs[1].Should().Contain("monitoring changed from disabled to enabled");
            }
            finally
            {
                loggingConfiguration.LoggingRules.Remove(loggingRule);
                loggingConfiguration.RemoveTarget(targetName);
                LogManager.ReconfigExistingLoggers();
            }
        }
    }
}
