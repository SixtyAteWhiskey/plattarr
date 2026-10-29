using System;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Hygiene;
using Radarr.Http.REST;

namespace NzbDrone.Api.Test.v3.Hygiene
{
    [TestFixture]
    public class HygieneRuleControllerFixture : TestBase<HygieneRuleController>
    {
        [Test]
        public void invalid_rule_returns_bad_request()
        {
            Mocker.GetMock<IHygieneRuleService>()
                .Setup(x => x.Upsert(It.IsAny<HygieneRule>()))
                .Throws(new ArgumentException("Invalid rule"));

            Action create = () => Subject.Create(new HygieneRule());

            create.Should().Throw<BadRequestException>();
        }

        [Test]
        public void missing_body_returns_bad_request()
        {
            Action create = () => Subject.Create(null);

            create.Should().Throw<BadRequestException>();
        }

        [Test]
        public void newly_created_rule_is_disabled_with_replacement_search_on_by_default()
        {
            HygieneRule saved = null;
            Mocker.GetMock<IHygieneRuleService>()
                .Setup(x => x.Upsert(It.IsAny<HygieneRule>()))
                .Callback<HygieneRule>(rule => saved = rule)
                .Returns<HygieneRule>(rule => rule);

            Subject.Create(new HygieneRule { ReasonCode = "download-stalled" });

            saved.Should().NotBeNull();
            saved.Enabled.Should().BeFalse();
            saved.SearchForReplacement.Should().BeTrue();
        }
    }
}
