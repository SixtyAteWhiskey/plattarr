using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;

namespace NzbDrone.Core.Test.TvTests.AutoTagging
{
    [TestFixture]
    public class TvAutoTagUsageSourceFixture : CoreTest<TvAutoTagUsageSource>
    {
        [Test]
        public void should_be_the_tv_source()
        {
            Subject.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_report_applied_tags_and_the_tags_the_conditions_look_for()
        {
            Mocker.GetMock<IAutoTaggingService>().Setup(a => a.All()).Returns(new List<AutoTag>
            {
                new AutoTag
                {
                    Id = 4,
                    Name = "A",
                    Tags = new HashSet<int> { 1, 2 },
                    Specifications = new List<IAutoTaggingSpecification>
                    {
                        new TagSpecification { Value = 8 },
                        new MonitoredSpecification(),
                        new TagSpecification { Value = 9 }
                    }
                }
            });

            var usage = Subject.All().Should().ContainSingle().Subject;

            usage.Id.Should().Be(4);
            usage.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
            usage.ConditionTags.Should().BeEquivalentTo(new[] { 8, 9 });
            usage.Uses(8).Should().BeTrue();
            usage.Uses(2).Should().BeTrue();
            usage.Uses(3).Should().BeFalse();
        }
    }
}
