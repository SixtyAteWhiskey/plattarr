using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.SeasonPass;
using Radarr.Api.V3.Tv.Series;
using TvSeries = NzbDrone.Core.Tv.Series;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class SeasonPassControllerFixture : TestBase<SeasonPassController>
    {
        private TvSeries _series;

        [SetUp]
        public void Setup()
        {
            _series = new TvSeries
            {
                Id = 7,
                Monitored = true,
                Seasons = new List<Season>
                {
                    new Season { SeasonNumber = 1, Monitored = true },
                    new Season { SeasonNumber = 2, Monitored = true }
                }
            };

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(It.IsAny<IEnumerable<int>>()))
                  .Returns(new List<TvSeries> { _series });
        }

        private SeasonPassResource Resource(MonitoringOptions options, SeasonPassSeriesResource series = null)
        {
            return new SeasonPassResource
            {
                Series = new List<SeasonPassSeriesResource> { series ?? new SeasonPassSeriesResource { Id = 7 } },
                MonitoringOptions = options
            };
        }

        [Test]
        public void should_pass_the_monitoring_options_to_the_monitored_service()
        {
            var options = new MonitoringOptions { Monitor = MonitorTypes.Future };

            Subject.UpdateAll(Resource(options));

            Mocker.GetMock<IEpisodeMonitoredService>()
                  .Verify(s => s.SetEpisodeMonitoredStatus(_series, options), Times.Once());
        }

        [Test]
        public void should_unmonitor_the_series_when_monitor_is_none()
        {
            Subject.UpdateAll(Resource(new MonitoringOptions { Monitor = MonitorTypes.None }));

            _series.Monitored.Should().BeFalse();
        }

        [Test]
        public void should_keep_the_series_monitored_for_other_monitor_types()
        {
            Subject.UpdateAll(Resource(new MonitoringOptions { Monitor = MonitorTypes.Missing }));

            _series.Monitored.Should().BeTrue();
        }

        [Test]
        public void should_apply_series_and_season_monitored_flags()
        {
            var series = new SeasonPassSeriesResource
            {
                Id = 7,
                Monitored = false,
                Seasons = new List<SeasonResource> { new SeasonResource { SeasonNumber = 2, Monitored = false } }
            };

            Subject.UpdateAll(Resource(null, series));

            _series.Monitored.Should().BeFalse();
            _series.Seasons.Find(s => s.SeasonNumber == 1).Monitored.Should().BeTrue();
            _series.Seasons.Find(s => s.SeasonNumber == 2).Monitored.Should().BeFalse();
            Mocker.GetMock<IEpisodeMonitoredService>()
                  .Verify(s => s.SetEpisodeMonitoredStatus(_series, null), Times.Once());
        }
    }
}
