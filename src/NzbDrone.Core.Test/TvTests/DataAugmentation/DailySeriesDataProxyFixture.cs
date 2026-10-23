using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.DataAugmentation.DailySeries;
using NzbDrone.Test.Common.Categories;

namespace NzbDrone.Core.Test.TvTests.DataAugmentation
{
    [TestFixture]
    [IntegrationTest]
    public class DailySeriesDataProxyFixture : CoreTest<DailySeriesDataProxy>
    {
        [SetUp]
        public void Setup()
        {
            UseRealHttp();
        }

        [Test]
        public void should_get_list_of_daily_series()
        {
            var list = Subject.GetDailySeriesIds();
            list.Should().NotBeEmpty();
            list.Should().OnlyHaveUniqueItems();
        }
    }
}
