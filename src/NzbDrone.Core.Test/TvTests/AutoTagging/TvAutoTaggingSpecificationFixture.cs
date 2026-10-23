using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.AutoTagging.Specifications;

namespace NzbDrone.Core.Test.TvTests.AutoTagging
{
    // One test per Sonarr specification: what it matches, and what it validates.
    [TestFixture]
    public class TvAutoTaggingSpecificationFixture
    {
        private static Series NewSeries()
        {
            return new Series
            {
                Genres = new List<string> { "Drama", "Crime" },
                Network = "AMC",
                OriginalCountry = "USA",
                OriginalLanguage = Language.French,
                Year = 2008,
                Status = SeriesStatusType.Ended,
                SeriesType = SeriesTypes.Anime,
                QualityProfileId = 4,
                Monitored = true,
                RootFolderPath = "/tv",
                Tags = new HashSet<int> { 3 }
            };
        }

        [Test]
        public void genre_should_match_any_listed_genre_ignoring_case()
        {
            new GenreSpecification { Value = new[] { "crime", "Comedy" } }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new GenreSpecification { Value = new[] { "Comedy" } }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void network_should_match_ignoring_case()
        {
            new NetworkSpecification { Value = new[] { "HBO", "amc" } }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new NetworkSpecification { Value = new[] { "HBO" } }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void original_country_should_match_ignoring_case()
        {
            new OriginalCountrySpecification { Value = new[] { "usa" } }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new OriginalCountrySpecification { Value = new[] { "gbr" } }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void original_country_should_need_three_character_codes()
        {
            new OriginalCountrySpecification { Value = new[] { "usa" } }.Validate().IsValid.Should().BeTrue();
            new OriginalCountrySpecification { Value = new[] { "us" } }.Validate().IsValid.Should().BeFalse();
        }

        [Test]
        public void original_language_should_match_by_id()
        {
            new OriginalLanguageSpecification { Value = Language.French.Id }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new OriginalLanguageSpecification { Value = Language.English.Id }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void quality_profile_should_match_by_id_and_need_a_positive_one()
        {
            new QualityProfileSpecification { Value = 4 }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new QualityProfileSpecification { Value = 5 }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
            new QualityProfileSpecification { Value = 0 }.Validate().IsValid.Should().BeFalse();
        }

        [Test]
        public void root_folder_should_match_the_series_root_folder()
        {
            new RootFolderSpecification { Value = "/tv" }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new RootFolderSpecification { Value = "/anime" }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void series_type_should_match_the_type()
        {
            new SeriesTypeSpecification { Value = (int)SeriesTypes.Anime }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new SeriesTypeSpecification { Value = (int)SeriesTypes.Daily }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
            new SeriesTypeSpecification { Value = 99 }.Validate().IsValid.Should().BeFalse();
        }

        [Test]
        public void status_should_match_the_status()
        {
            new StatusSpecification { Status = (int)SeriesStatusType.Ended }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new StatusSpecification { Status = (int)SeriesStatusType.Continuing }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void year_should_match_within_the_inclusive_range()
        {
            new YearSpecification { Min = 2008, Max = 2008 }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new YearSpecification { Min = 2000, Max = 2007 }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
            new YearSpecification { Min = 2009, Max = 2010 }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void year_should_need_a_range_that_is_not_backwards()
        {
            new YearSpecification { Min = 2010, Max = 2000 }.Validate().IsValid.Should().BeFalse();
            new YearSpecification { Min = 0, Max = 2000 }.Validate().IsValid.Should().BeFalse();
        }

        [Test]
        public void monitored_should_match_the_flag()
        {
            new MonitoredSpecification().IsSatisfiedBy(NewSeries()).Should().BeTrue();

            var unmonitored = NewSeries();
            unmonitored.Monitored = false;
            new MonitoredSpecification().IsSatisfiedBy(unmonitored).Should().BeFalse();
        }

        [Test]
        public void tag_should_match_a_series_tag_and_need_a_positive_id()
        {
            new TagSpecification { Value = 3 }.IsSatisfiedBy(NewSeries()).Should().BeTrue();
            new TagSpecification { Value = 4 }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
            new TagSpecification { Value = 0 }.Validate().IsValid.Should().BeFalse();
        }

        [Test]
        public void negate_should_flip_the_result()
        {
            new MonitoredSpecification { Negate = true }.IsSatisfiedBy(NewSeries()).Should().BeFalse();
        }

        [Test]
        public void genre_network_and_country_should_need_a_value()
        {
            new GenreSpecification { Value = Array.Empty<string>() }.Validate().IsValid.Should().BeFalse();
            new NetworkSpecification { Value = Array.Empty<string>() }.Validate().IsValid.Should().BeFalse();
            new OriginalCountrySpecification { Value = Array.Empty<string>() }.Validate().IsValid.Should().BeFalse();
        }
    }
}
