using System.Collections.Generic;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.Events;
using NzbDrone.Core.Tv.MetadataSource;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class RefreshSeriesStartingEventFixture : CoreTest<RefreshSeriesService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Language = Language.English)
                                     .With(s => s.SeasonType = SeasonType.Official)
                                     .With(s => s.Seasons = new List<Season>())
                                     .Build();

            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(_series.Id)).Returns(_series);

            // A series TheTVDB no longer has: the refresh must still announce itself.
            Mocker.GetMock<IProvideSeriesInfo>()
                  .Setup(s => s.GetSeriesInfo(It.IsAny<int>(), It.IsAny<Language>(), It.IsAny<string>()))
                  .Callback<int, Language, string>((p, _, _) => { throw new SeriesNotFoundException(p); });
        }

        [TestCase(CommandTrigger.Manual, true)]
        [TestCase(CommandTrigger.Scheduled, false)]
        public void should_publish_refresh_starting_with_the_trigger(CommandTrigger trigger, bool expectedManual)
        {
            var command = new RefreshSeriesCommand(new List<int> { _series.Id }) { Trigger = trigger };

            Subject.Execute(command);

            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<SeriesRefreshStartingEvent>(ev => ev.ManualTrigger == expectedManual)), Times.Once());
        }
    }
}
