using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.History;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class HistoryServiceFixture : CoreTest<HistoryService>
    {
        [Test]
        public void should_find_only_the_history_of_the_event_type_for_a_download_id()
        {
            Mocker.GetMock<IHistoryRepository>()
                  .Setup(v => v.FindByDownloadId("id"))
                  .Returns(new List<EpisodeHistory>
                  {
                      new EpisodeHistory { Id = 1, EventType = EpisodeHistoryEventType.Grabbed },
                      new EpisodeHistory { Id = 2, EventType = EpisodeHistoryEventType.DownloadFailed },
                      new EpisodeHistory { Id = 3, EventType = EpisodeHistoryEventType.Grabbed }
                  });

            Subject.Find("id", EpisodeHistoryEventType.Grabbed).Should().HaveCount(2).And.OnlyContain(h => h.EventType == EpisodeHistoryEventType.Grabbed);
            Subject.FindByDownloadId("id").Should().HaveCount(3);
        }

        [Test]
        public void should_read_through_the_repository()
        {
            var since = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var recent = new EpisodeHistory { Id = 9 };

            Mocker.GetMock<IHistoryRepository>().Setup(v => v.MostRecentForEpisode(5)).Returns(recent);
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.MostRecentForDownloadId("id")).Returns(recent);
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.Get(9)).Returns(recent);
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.FindByEpisodeId(5)).Returns(new List<EpisodeHistory> { recent });
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.GetBySeries(3, EpisodeHistoryEventType.Grabbed)).Returns(new List<EpisodeHistory> { recent });
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.GetBySeason(3, 2, null)).Returns(new List<EpisodeHistory> { recent });
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.GetByEpisode(5, null)).Returns(new List<EpisodeHistory> { recent });
            Mocker.GetMock<IHistoryRepository>().Setup(v => v.Since(since, null)).Returns(new List<EpisodeHistory> { recent });

            Subject.MostRecentForEpisode(5).Should().BeSameAs(recent);
            Subject.MostRecentForDownloadId("id").Should().BeSameAs(recent);
            Subject.Get(9).Should().BeSameAs(recent);
            Subject.FindByEpisodeId(5).Should().ContainSingle();
            Subject.GetBySeries(3, EpisodeHistoryEventType.Grabbed).Should().ContainSingle();
            Subject.GetBySeason(3, 2, null).Should().ContainSingle();
            Subject.GetByEpisode(5, null).Should().ContainSingle();
            Subject.Since(since, null).Should().ContainSingle();
        }
    }
}
