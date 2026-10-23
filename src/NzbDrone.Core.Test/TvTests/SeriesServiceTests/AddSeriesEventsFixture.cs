using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Events;

namespace NzbDrone.Core.Test.TvTests.SeriesServiceTests
{
    [TestFixture]
    public class AddSeriesEventsFixture : CoreTest<SeriesService>
    {
        [Test]
        public void should_publish_series_added_with_the_stored_series_when_adding_one()
        {
            var series = Builder<Series>.CreateNew().With(s => s.Id = 42).Build();
            var stored = Builder<Series>.CreateNew().With(s => s.Id = 42).With(s => s.Title = "Stored").Build();

            Mocker.GetMock<ISeriesRepository>().Setup(r => r.Get(42)).Returns(stored);

            Subject.AddSeries(series);

            Mocker.GetMock<ISeriesRepository>().Verify(r => r.Insert(series), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<SeriesAddedEvent>(ev => ReferenceEquals(ev.Series, stored))), Times.Once());
        }

        [Test]
        public void should_publish_series_imported_with_every_new_id_when_adding_many()
        {
            var series = Builder<Series>.CreateListOfSize(3).Build().ToList();

            Subject.AddSeries(series);

            Mocker.GetMock<ISeriesRepository>().Verify(r => r.InsertMany(series), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.Is<SeriesImportedEvent>(ev => ev.SeriesIds.SequenceEqual(series.Select(s => s.Id)))), Times.Once());
            Mocker.GetMock<IEventAggregator>()
                  .Verify(e => e.PublishEvent(It.IsAny<SeriesAddedEvent>()), Times.Never());
        }
    }
}
