using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.AutoTagging;

namespace NzbDrone.Core.Test.TvTests.SeriesServiceTests
{
    [TestFixture]
    public class AutoTaggingFixture : CoreTest<SeriesService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                .With(s => s.Tags = new HashSet<int> { 1, 2 })
                .With(s => s.RootFolderPath = "")
                .With(s => s.PendingPath = null)
                .Build();

            Mocker.GetMock<ISeriesRepository>().Setup(r => r.Get(It.IsAny<int>())).Returns(_series);
            Mocker.GetMock<ISeriesRepository>().Setup(r => r.Update(It.IsAny<Series>())).Returns<Series>(s => s);
            Mocker.GetMock<IManageCommandQueue>().Setup(q => q.All()).Returns(new List<CommandModel>());
        }

        private void GivenChanges(IEnumerable<int> add, IEnumerable<int> remove)
        {
            Mocker.GetMock<IAutoTaggingService>()
                  .Setup(a => a.GetTagChanges(It.IsAny<Series>()))
                  .Returns(new AutoTaggingChanges
                  {
                      TagsToAdd = add.ToHashSet(),
                      TagsToRemove = remove.ToHashSet()
                  });
        }

        [Test]
        public void should_add_and_remove_tags_and_say_something_changed()
        {
            GivenChanges(new[] { 3 }, new[] { 1 });

            Subject.UpdateAutoTaggingTags(_series).Should().BeTrue();

            _series.Tags.Should().BeEquivalentTo(new[] { 2, 3 });
        }

        [Test]
        public void should_say_nothing_changed_when_the_series_already_has_every_tag_and_lacks_the_removed_ones()
        {
            GivenChanges(new[] { 2 }, new[] { 9 });

            Subject.UpdateAutoTaggingTags(_series).Should().BeFalse();

            _series.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Test]
        public void should_say_nothing_changed_when_there_are_no_changes()
        {
            GivenChanges(Array.Empty<int>(), Array.Empty<int>());

            Subject.UpdateAutoTaggingTags(_series).Should().BeFalse();
        }

        [Test]
        public void should_apply_auto_tags_before_saving_a_single_series()
        {
            GivenChanges(new[] { 5 }, Array.Empty<int>());

            Subject.UpdateSeries(_series);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(r => r.Update(It.Is<Series>(s => s.Tags.Contains(5))), Times.Once());
        }

        [Test]
        public void should_apply_auto_tags_to_every_series_in_a_bulk_update()
        {
            var many = Builder<Series>.CreateListOfSize(3)
                .All()
                .With(s => s.Tags = new HashSet<int>())
                .With(s => s.RootFolderPath = "")
                .Build().ToList();
            GivenChanges(new[] { 7 }, Array.Empty<int>());

            Subject.UpdateSeries(many, false);

            many.Should().OnlyContain(s => s.Tags.Contains(7));
            Mocker.GetMock<ISeriesRepository>().Verify(r => r.UpdateMany(many), Times.Once());
        }

        [Test]
        public void should_not_apply_auto_tags_when_only_setting_the_tags_in_bulk()
        {
            GivenChanges(new[] { 5 }, Array.Empty<int>());

            Subject.UpdateTags(new List<Series> { _series });

            Mocker.GetMock<IAutoTaggingService>().Verify(a => a.GetTagChanges(It.IsAny<Series>()), Times.Never());
            _series.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
        }
    }
}
