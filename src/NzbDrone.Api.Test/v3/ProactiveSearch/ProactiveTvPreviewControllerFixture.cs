using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.History;
using NzbDrone.Test.Common;
using Radarr.Api.V3.ProactiveSearch;

namespace NzbDrone.Api.Test.v3.ProactiveSearch
{
    [TestFixture]
    public class ProactiveTvPreviewControllerFixture : TestBase
    {
        [Test]
        public void preview_groups_missing_episodes_and_limits_skipped_series()
        {
            var episodes = new List<Episode>
            {
                new() { Id = 11, SeriesId = 1, Monitored = true },
                new() { Id = 12, SeriesId = 1, Monitored = true },
                new() { Id = 21, SeriesId = 2, Monitored = true }
            };
            Mocker.GetMock<IEpisodeService>()
                .Setup(x => x.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, It.IsAny<HashSet<int>>()))
                .Returns(new PagingSpec<Episode> { Records = episodes });
            Mocker.GetMock<ISeriesService>().Setup(x => x.GetAllSeries()).Returns(new List<Series>
            {
                new() { Id = 1, Title = "Alpha", Monitored = true },
                new() { Id = 2, Title = "Beta", Monitored = false }
            });
            Mocker.GetMock<IQueueService>().Setup(x => x.GetQueue()).Returns(new List<NzbDrone.Core.Queue.Queue>());
            Mocker.GetMock<IHistoryService>()
                .Setup(x => x.Since(It.IsAny<DateTime>(), EpisodeHistoryEventType.DownloadFailed))
                .Returns(new List<EpisodeHistory>());
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvSearchEnabled).Returns(true);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvActiveDownloadLimit).Returns(5);
            Mocker.GetMock<IConfigService>().SetupGet(x => x.ProactiveTvRecentGrabHours).Returns(24);

            var preview = new ProactiveTvPreviewController(
                Mocker.GetMock<IEpisodeService>().Object,
                Mocker.GetMock<ISeriesService>().Object,
                Mocker.GetMock<IQueueService>().Object,
                Mocker.GetMock<IHistoryService>().Object,
                Mocker.GetMock<IConfigService>().Object).Get(0, 50, null).Value;

            preview.MissingSeries.Should().Be(2);
            preview.MissingEpisodes.Should().Be(3);
            preview.EligibleSeries.Should().Be(1);
            preview.CandidateSeries.Should().ContainSingle().Which.Title.Should().Be("Alpha");
            preview.SkippedSeries.Should().ContainSingle().Which.Title.Should().Be("Beta");
            preview.SkippedSeries[0].Reasons["unmonitored"].Should().Be(1);
        }
    }
}
