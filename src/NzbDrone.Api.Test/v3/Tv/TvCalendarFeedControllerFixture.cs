using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.Calendar;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvCalendarFeedControllerFixture : TestBase<TvCalendarFeedController>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetAllSeries())
                  .Returns(new List<Series> { new Series { Id = 1, Title = "Game of Thrones", Network = "HBO", Runtime = 60, Tags = new HashSet<int>() } });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.EpisodesBetweenDates(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Returns(new List<Episode>
                  {
                      new Episode { Id = 7, SeriesId = 1, Title = "Winter Is Coming", SeasonNumber = 1, EpisodeNumber = 1, AirDateUtc = new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc) }
                  });
        }

        private string Feed(bool asAllDay)
        {
            var result = Subject.GetCalendarFeed(asAllDay: asAllDay);

            return result.Result.Should().BeOfType<ContentHttpResult>().Subject.ResponseContent;
        }

        [Test]
        public void should_write_a_timed_event_by_default()
        {
            var feed = Feed(false);

            feed.Should().Contain("UID:NzbDrone_episode_7");
            feed.Should().Contain("SUMMARY:Game of Thrones - 1x01 - Winter Is Coming");
            feed.Should().Contain("DTSTART:20261101T010000Z");
        }

        [Test]
        public void should_write_an_all_day_event_when_asked_without_failing_on_a_local_time()
        {
            var feed = Feed(true);

            feed.Should().Contain("UID:NzbDrone_episode_7");
            feed.Should().Contain("DTSTART;VALUE=DATE:" + new DateTime(2026, 11, 1, 1, 0, 0, DateTimeKind.Utc).ToLocalTime().ToString("yyyyMMdd"));
        }
    }
}
