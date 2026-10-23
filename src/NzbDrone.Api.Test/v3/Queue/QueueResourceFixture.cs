using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Queue;

namespace NzbDrone.Api.Test.v3.Queue
{
    [TestFixture]
    public class QueueResourceFixture : TestBase
    {
        private CustomFormat _format;
        private QualityProfile _profile;
        private Series _series;
        private Episode _episode;

        [SetUp]
        public void Setup()
        {
            _format = new CustomFormat("Test") { Id = 7 };

            _profile = new QualityProfile
            {
                Name = "Test",
                Items = new List<QualityProfileQualityItem>(),
                FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = _format, Score = 25 } }
            };

            _series = new Series
            {
                Id = 3,
                Title = "Series",
                QualityProfileId = 1,
                QualityProfile = new LazyLoaded<QualityProfile>(_profile)
            };

            _episode = new Episode { Id = 40, SeriesId = 3, SeasonNumber = 2, EpisodeNumber = 5, EpisodeFileId = 12 };
        }

        private NzbDrone.Core.Queue.Queue GivenTvItem()
        {
            return new NzbDrone.Core.Queue.Queue
            {
                Id = 9,
                Title = "Series.S02E05.1080p",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteEpisode
                {
                    Series = _series,
                    Episodes = new List<Episode> { _episode },
                    CustomFormats = new List<CustomFormat> { _format }
                }
            };
        }

        [Test]
        public void should_map_the_series_and_episode_of_a_tv_download()
        {
            var resource = GivenTvItem().ToResource(false);

            resource.SeriesId.Should().Be(3);
            resource.EpisodeId.Should().Be(40);
            resource.SeasonNumber.Should().Be(2);
            resource.EpisodeHasFile.Should().BeTrue();
            resource.MovieId.Should().BeNull();
        }

        [Test]
        public void should_score_a_tv_download_with_the_series_quality_profile()
        {
            var resource = GivenTvItem().ToResource(false);

            resource.CustomFormatScore.Should().Be(25);
            resource.CustomFormats.Should().HaveCount(1);
        }

        [Test]
        public void should_include_series_and_episode_only_when_asked()
        {
            GivenTvItem().ToResource(false).Series.Should().BeNull();
            GivenTvItem().ToResource(false).Episode.Should().BeNull();

            var resource = GivenTvItem().ToResource(false, true, true);

            resource.Series.Should().NotBeNull();
            resource.Series.Title.Should().Be("Series");
            resource.Episode.Should().NotBeNull();
            resource.Episode.EpisodeNumber.Should().Be(5);
        }

        [Test]
        public void should_not_write_tv_fields_for_a_movie_download()
        {
            var item = new NzbDrone.Core.Queue.Queue
            {
                Id = 4,
                Title = "Movie.2020.1080p",
                Quality = new QualityModel(Quality.WEBDL1080p),
                Languages = new List<NzbDrone.Core.Languages.Language>(),
                RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 8 } }
            };

            var json = STJson.ToJson(item.ToResource(false));

            json.Should().Contain("\"movieId\"");
            json.Should().NotContain("seriesId");
            json.Should().NotContain("episodeId");
            json.Should().NotContain("seasonNumber");
            json.Should().NotContain("episodeHasFile");
        }

        [Test]
        public void is_matched_should_be_true_for_a_series_or_a_movie_and_false_for_neither()
        {
            GivenTvItem().IsMatched().Should().BeTrue();

            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteMovie { Movie = new Movie { Id = 1 } } }.IsMatched().Should().BeTrue();
            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteEpisode() }.IsMatched().Should().BeFalse();
            new NzbDrone.Core.Queue.Queue { RemoteRelease = new RemoteMovie() }.IsMatched().Should().BeFalse();
            new NzbDrone.Core.Queue.Queue().IsMatched().Should().BeFalse();
        }
    }
}
