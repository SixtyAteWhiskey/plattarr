using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;
using MoviePendingRelease = NzbDrone.Core.Movies.Pending.PendingRelease;
using MoviePendingReleaseRepository = NzbDrone.Core.Movies.Pending.PendingReleaseRepository;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class PendingReleaseRepositoryFixture : DbTest<PendingReleaseRepository, PendingRelease>
    {
        private Series _series;
        private Series _other;

        [SetUp]
        public void Setup()
        {
            var seriesRepository = Mocker.Resolve<SeriesRepository>();

            _series = TvTestData.NewSeries(81189, "Breaking Bad");
            _other = TvTestData.NewSeries(121361, "Game Of Thrones");
            seriesRepository.Insert(_series);
            seriesRepository.Insert(_other);
        }

        private static PendingRelease NewPending(Series series, string title = "Show.S01E01.720p-GRP", PendingReleaseReason reason = PendingReleaseReason.Delay)
        {
            return new PendingRelease
            {
                MediaKind = MediaKind.Tv,
                ParentId = series.Id,
                Title = title,
                Added = DateTime.UtcNow,
                Reason = reason,
                ParsedInfo = new ParsedEpisodeInfo
                {
                    SeriesTitle = "Show",
                    ReleaseTitle = title,
                    SeasonNumbers = new List<int> { 1 },
                    EpisodeNumbers = new[] { 1, 2 },
                    AbsoluteEpisodeNumbers = new[] { 101 },
                    Quality = new QualityModel(Quality.HDTV720p, new Revision(2)),
                    Languages = new List<Language> { Language.English, Language.French },
                    ReleaseGroup = "GRP"
                },
                Release = new ReleaseInfo { Title = title, Indexer = "idx", Size = 1234 },
                AdditionalInfo = new PendingReleaseAdditionalInfo
                {
                    SeriesMatchType = SeriesMatchType.Alias,
                    ReleaseSource = ReleaseSourceType.Rss
                }
            };
        }

        [Test]
        public void should_round_trip_the_parsed_episode_info_the_release_and_the_match_information()
        {
            var pending = NewPending(_series);

            Subject.Insert(pending);

            var stored = Subject.Get(pending.Id);

            stored.MediaKind.Should().Be(MediaKind.Tv);
            stored.ParentId.Should().Be(_series.Id);
            stored.Reason.Should().Be(PendingReleaseReason.Delay);
            stored.ParsedInfo.SeriesTitle.Should().Be("Show");
            stored.ParsedInfo.SeasonNumbers.Should().Equal(1);
            stored.ParsedInfo.EpisodeNumbers.Should().Equal(1, 2);
            stored.ParsedInfo.AbsoluteEpisodeNumbers.Should().Equal(101);
            stored.ParsedInfo.Quality.Quality.Should().Be(Quality.HDTV720p);
            stored.ParsedInfo.Quality.Revision.Version.Should().Be(2);
            stored.ParsedInfo.Languages.Should().Equal(Language.English, Language.French);
            stored.ParsedInfo.ReleaseGroup.Should().Be("GRP");
            stored.Release.Title.Should().Be("Show.S01E01.720p-GRP");
            stored.Release.Size.Should().Be(1234);
            stored.AdditionalInfo.SeriesMatchType.Should().Be(SeriesMatchType.Alias);
            stored.AdditionalInfo.ReleaseSource.Should().Be(ReleaseSourceType.Rss);
            stored.RemoteEpisode.Should().BeNull();
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var pending = NewPending(_series);
            pending.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(pending);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_get_and_delete_by_series_id()
        {
            Subject.Insert(NewPending(_series, "A"));
            Subject.Insert(NewPending(_series, "B"));
            Subject.Insert(NewPending(_other, "C"));

            Subject.AllBySeriesId(_series.Id).Select(p => p.Title).Should().BeEquivalentTo("A", "B");

            Subject.DeleteBySeriesIds(new List<int> { _series.Id });

            Subject.AllBySeriesId(_series.Id).Should().BeEmpty();
            Subject.AllBySeriesId(_other.Id).Should().ContainSingle();
        }

        [Test]
        public void should_not_return_fallback_releases_without_a_series()
        {
            Subject.Insert(NewPending(_series, "A", PendingReleaseReason.Delay));
            Subject.Insert(NewPending(_series, "B", PendingReleaseReason.Fallback));
            Subject.Insert(NewPending(_series, "C", PendingReleaseReason.DownloadClientUnavailable));

            var orphan = NewPending(_series, "D");
            orphan.ParentId = 9999;
            Subject.Insert(orphan);

            Subject.WithoutFallback().Select(p => p.Title).Should().BeEquivalentTo("A", "C");
        }

        [Test]
        public void should_not_see_or_delete_movie_rows_that_share_a_parent_id()
        {
            var movieRepository = Mocker.Resolve<MoviePendingReleaseRepository>();

            movieRepository.Insert(new MoviePendingRelease
            {
                MediaKind = MediaKind.Movies,
                ParentId = _series.Id,
                Title = "Movie",
                Added = DateTime.UtcNow,
                Release = new ReleaseInfo()
            });
            Subject.Insert(NewPending(_series));

            Subject.All().Should().ContainSingle();
            Subject.AllBySeriesId(_series.Id).Should().ContainSingle();
            Subject.WithoutFallback().Should().ContainSingle();

            Subject.DeleteBySeriesIds(new List<int> { _series.Id });

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }
    }
}
