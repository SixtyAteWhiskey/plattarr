using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Test.TvTests.AutoTagging
{
    [TestFixture]
    public class TvAutoTaggingServiceFixture : CoreTest<AutoTaggingService>
    {
        private Series _series;
        private AutoTag _autoTag;

        [SetUp]
        public void Setup()
        {
            _series = new Series
            {
                Title = "Breaking Bad",
                Path = "/tv/Breaking Bad",
                Genres = new List<string> { "Drama", "Crime" },
                Network = "AMC",
                OriginalCountry = "usa",
                OriginalLanguage = Language.English,
                Year = 2008,
                Status = SeriesStatusType.Ended,
                SeriesType = SeriesTypes.Standard,
                QualityProfileId = 4,
                Monitored = true,
                Tags = new HashSet<int>()
            };

            _autoTag = new AutoTag
            {
                Id = 1,
                Name = "Test",
                Specifications = new List<IAutoTaggingSpecification>
                {
                    new GenreSpecification { Name = "Genre", Value = new List<string> { "Drama" } }
                },
                Tags = new HashSet<int> { 1 },
                RemoveTagsAutomatically = false
            };
        }

        private void GivenAutoTags(params AutoTag[] autoTags)
        {
            Mocker.GetMock<IAutoTaggingRepository>().Setup(s => s.All()).Returns(autoTags.ToList());
        }

        [Test]
        public void should_not_have_changes_if_there_are_no_auto_tags()
        {
            GivenAutoTags();

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().BeEmpty();
            result.TagsToRemove.Should().BeEmpty();
        }

        [Test]
        public void should_have_tags_to_add_if_the_series_matches_and_lacks_the_tag()
        {
            GivenAutoTags(_autoTag);

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().Equal(1);
            result.TagsToRemove.Should().BeEmpty();
        }

        [Test]
        public void should_not_add_a_tag_the_series_already_has()
        {
            _series.Tags = new HashSet<int> { 1 };
            GivenAutoTags(_autoTag);

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().BeEmpty();
            result.TagsToRemove.Should().BeEmpty();
        }

        [Test]
        public void should_not_remove_the_tag_when_the_series_stops_matching_unless_remove_is_on()
        {
            _series.Tags = new HashSet<int> { 1 };
            _series.Genres = new List<string> { "Comedy" };
            GivenAutoTags(_autoTag);

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().BeEmpty();
            result.TagsToRemove.Should().BeEmpty();
        }

        [Test]
        public void should_remove_the_tag_when_the_series_stops_matching_and_remove_is_on()
        {
            _series.Tags = new HashSet<int> { 1 };
            _series.Genres = new List<string> { "Comedy" };
            _autoTag.RemoveTagsAutomatically = true;
            GivenAutoTags(_autoTag);

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().BeEmpty();
            result.TagsToRemove.Should().Equal(1);
        }

        [Test]
        public void should_match_a_negated_specification_when_the_series_does_not_match_it()
        {
            _series.Genres = new List<string> { "Comedy" };
            _autoTag.Specifications.First().Negate = true;
            GivenAutoTags(_autoTag);

            Subject.GetTagChanges(_series).TagsToAdd.Should().Equal(1);
        }

        [Test]
        public void should_need_one_match_per_specification_type_unless_a_specification_is_required()
        {
            // Two genre specifications are one group: any of them is enough...
            _autoTag.Specifications = new List<IAutoTaggingSpecification>
            {
                new GenreSpecification { Name = "A", Value = new List<string> { "Comedy" } },
                new GenreSpecification { Name = "B", Value = new List<string> { "Drama" } }
            };
            GivenAutoTags(_autoTag);

            Subject.GetTagChanges(_series).TagsToAdd.Should().Equal(1);

            // ...until one of them is required.
            _autoTag.Specifications = new List<IAutoTaggingSpecification>
            {
                new GenreSpecification { Name = "A", Value = new List<string> { "Comedy" }, Required = true },
                new GenreSpecification { Name = "B", Value = new List<string> { "Drama" } }
            };

            Subject.GetTagChanges(_series).TagsToAdd.Should().BeEmpty();
        }

        [Test]
        public void should_need_every_specification_type_to_match()
        {
            _autoTag.Specifications = new List<IAutoTaggingSpecification>
            {
                new GenreSpecification { Name = "Genre", Value = new List<string> { "Drama" } },
                new NetworkSpecification { Name = "Network", Value = new List<string> { "HBO" } }
            };
            GivenAutoTags(_autoTag);

            Subject.GetTagChanges(_series).TagsToAdd.Should().BeEmpty();
        }

        [Test]
        public void should_set_the_root_folder_on_the_series_before_matching()
        {
            Mocker.GetMock<IRootFolderService>().Setup(r => r.GetBestRootFolderPath("/tv/Breaking Bad")).Returns("/tv");
            _autoTag.Specifications = new List<IAutoTaggingSpecification>
            {
                new RootFolderSpecification { Name = "Root", Value = "/tv" }
            };
            GivenAutoTags(_autoTag);

            Subject.GetTagChanges(_series).TagsToAdd.Should().Equal(1);
            _series.RootFolderPath.Should().Be("/tv");
        }

        [Test]
        public void should_apply_every_matching_auto_tag_and_remove_for_the_ones_that_stopped_matching()
        {
            var other = new AutoTag
            {
                Id = 2,
                Name = "Other",
                Specifications = new List<IAutoTaggingSpecification> { new NetworkSpecification { Name = "N", Value = new List<string> { "HBO" } } },
                Tags = new HashSet<int> { 2, 3 },
                RemoveTagsAutomatically = true
            };
            GivenAutoTags(_autoTag, other);

            var result = Subject.GetTagChanges(_series);

            result.TagsToAdd.Should().Equal(1);
            result.TagsToRemove.Should().BeEquivalentTo(new[] { 2, 3 });
        }

        [Test]
        public void should_read_the_repository_once_until_something_changes()
        {
            GivenAutoTags(_autoTag);

            Subject.All();
            Subject.All();

            Mocker.GetMock<IAutoTaggingRepository>().Verify(r => r.All(), Times.Once());
        }

        [Test]
        public void should_publish_the_series_event_and_reload_after_insert_update_and_delete()
        {
            GivenAutoTags(_autoTag);
            Subject.All();

            Subject.Insert(_autoTag);
            Subject.All();
            Subject.Update(_autoTag);
            Subject.All();
            Subject.Delete(1);
            Subject.All();

            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<SeriesAutoTagsUpdatedEvent>()), Times.Exactly(3));
            Mocker.GetMock<IAutoTaggingRepository>().Verify(r => r.All(), Times.Exactly(4));
        }

        [Test]
        public void should_find_the_auto_tags_that_apply_a_tag()
        {
            var other = new AutoTag { Id = 2, Name = "Other", Specifications = _autoTag.Specifications, Tags = new HashSet<int> { 9 } };
            GivenAutoTags(_autoTag, other);

            Subject.AllForTag(9).Should().ContainSingle().Which.Name.Should().Be("Other");
            Subject.AllForTag(5).Should().BeEmpty();
        }
    }
}
