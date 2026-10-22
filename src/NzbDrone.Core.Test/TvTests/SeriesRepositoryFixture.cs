using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SeriesRepositoryFixture : DbTest<SeriesRepository, Series>
    {
        [Test]
        public void should_round_trip_every_series_column()
        {
            var series = TvTestData.NewSeries();

            Subject.Insert(series);

            var stored = Subject.Get(series.Id);

            stored.Should().BeEquivalentTo(
                series,
                o => o.Excluding(s => s.QualityProfile)
                      .Excluding(s => s.Translations)
                      .Excluding(s => s.RootFolderPath)
                      .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime>());

            stored.Language.Should().Be(NzbDrone.Core.Languages.Language.English);
            stored.OriginalLanguage.Should().Be(NzbDrone.Core.Languages.Language.French);
        }

        [Test]
        public void should_round_trip_a_series_with_nothing_optional_set()
        {
            var series = new Series
            {
                TvdbId = 1,
                Title = "Bare",
                Path = "/data/tv/Bare",
                TitleSlug = "bare",
                Added = DateTime.UtcNow,
                Language = NzbDrone.Core.Languages.Language.English
            };

            Subject.Insert(series);

            var stored = Subject.Get(series.Id);

            stored.Title.Should().Be("Bare");
            stored.Images.Should().BeEmpty();
            stored.Seasons.Should().BeEmpty();
            stored.Tags.Should().BeEmpty();
            stored.Actors.Should().BeEmpty();
            stored.Genres.Should().BeEmpty();
            stored.AddOptions.Should().BeNull();
            stored.LastInfoSync.Should().BeNull();
        }

        [Test]
        public void should_find_by_tvdb_imdb_and_path()
        {
            Subject.Insert(TvTestData.NewSeries(81189, "Breaking Bad"));
            Subject.Insert(TvTestData.NewSeries(121361, "Game Of Thrones"));

            Subject.FindByTvdbId(121361).Title.Should().Be("Game Of Thrones");
            Subject.FindByTvdbId(1).Should().BeNull();
            Subject.FindByImdbId("tt0081189").TvdbId.Should().Be(81189);
            Subject.FindByImdbId("tt0000000").Should().BeNull();
            Subject.FindByPath("/data/tv/Breaking Bad").TvdbId.Should().Be(81189);
            Subject.SeriesPathExists("/data/tv/Game Of Thrones").Should().BeTrue();
            Subject.SeriesPathExists("/data/tv/Nothing").Should().BeFalse();
        }

        [Test]
        public void should_find_by_clean_title_and_year()
        {
            var one = TvTestData.NewSeries(1, "Show One");
            one.Year = 2001;
            var two = TvTestData.NewSeries(2, "Show One Again");
            two.CleanTitle = one.CleanTitle;
            two.CleanOriginalTitle = "other";
            two.Year = 2002;

            Subject.Insert(one);
            Subject.Insert(two);

            Subject.FindByTitle("SHOWONE", 2002).TvdbId.Should().Be(2);
            Subject.FindByTitle("showone", 2001).TvdbId.Should().Be(1);
            Subject.FindByTitle("showone", 1999).Should().BeNull();
        }

        [Test]
        public void should_throw_when_a_title_matches_two_series()
        {
            var one = TvTestData.NewSeries(1, "Show One");
            var two = TvTestData.NewSeries(2, "Show Two");
            two.CleanTitle = one.CleanTitle;
            two.CleanOriginalTitle = one.CleanTitle;
            one.CleanOriginalTitle = one.CleanTitle;

            Subject.Insert(one);
            Subject.Insert(two);

            var action = () => Subject.FindByTitle(one.CleanTitle);

            action.Should().Throw<MultipleSeriesFoundException>();
        }

        [Test]
        public void should_find_a_series_whose_title_is_inside_the_given_text()
        {
            Subject.Insert(TvTestData.NewSeries(81189, "Breaking Bad"));
            Subject.Insert(TvTestData.NewSeries(121361, "Game Of Thrones"));

            Subject.FindByTitleInexact("breakingbad.s01e01.720p").Select(s => s.TvdbId).Should().Equal(81189);
            Subject.FindByTitleInexact("nothing.matches.here").Should().BeEmpty();
        }

        [Test]
        public void should_list_ids_paths_tags_and_profiles_by_series_id()
        {
            var one = TvTestData.NewSeries(81189, "Breaking Bad", 7);
            var two = TvTestData.NewSeries(121361, "Game Of Thrones", 8);
            two.Tags = new HashSet<int>();

            Subject.Insert(one);
            Subject.Insert(two);

            Subject.AllSeriesTvdbIds().Should().BeEquivalentTo(new Dictionary<int, int> { { one.Id, 81189 }, { two.Id, 121361 } });
            Subject.AllSeriesPaths().Should().BeEquivalentTo(new Dictionary<int, string> { { one.Id, one.Path }, { two.Id, two.Path } });
            Subject.AllSeriesQualityProfiles().Should().BeEquivalentTo(new Dictionary<int, int> { { one.Id, 7 }, { two.Id, 8 } });
            Subject.AllSeriesTags()[one.Id].Should().BeEquivalentTo(new[] { 3, 5 });
        }

        [Test]
        public void should_refuse_a_second_series_with_the_same_tvdb_id()
        {
            Subject.Insert(TvTestData.NewSeries(81189, "Breaking Bad"));

            var duplicate = TvTestData.NewSeries(81189, "Breaking Bad Again");

            var action = () => Subject.Insert(duplicate);

            action.Should().Throw<Exception>();
        }

        [Test]
        public void should_load_the_quality_profile()
        {
            var profileRepository = Mocker.Resolve<QualityProfileRepository>();
            Mocker.SetConstant<IQualityProfileRepository>(profileRepository);

            var profile = new QualityProfile
            {
                Items = Qualities.QualityFixture.GetDefaultQualities(Quality.Bluray1080p, Quality.DVD),
                FormatItems = CustomFormats.CustomFormatsTestHelpers.GetDefaultFormatItems(),
                MinFormatScore = 0,
                Cutoff = Quality.Bluray1080p.Id,
                Name = "TvProfile"
            };

            profileRepository.Insert(profile);

            Subject.Insert(TvTestData.NewSeries(81189, "Breaking Bad", profile.Id));

            Subject.All().Single().QualityProfile.Value.Name.Should().Be("TvProfile");
        }
    }
}
