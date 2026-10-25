using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Notifications;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class TvActivityRendererFixture : CoreTest<TvActivityRenderer>
    {
        private Series _series;
        private List<Episode> _episodes;

        [SetUp]
        public void Setup()
        {
            _series = TvGoldenData.Series();
            _episodes = TvGoldenData.Episodes(_series);
        }

        [Test]
        public void should_return_null_for_a_missing_series()
        {
            Subject.Describe((Series)null, _episodes).Should().BeNull();
        }

        [Test]
        public void should_describe_the_series_as_a_subject()
        {
            var subject = Subject.Describe(_series, _episodes);

            subject.Kind.Should().Be(MediaKind.Tv);
            subject.KindLabel.Should().Be("TV Show");
            subject.ItemKindLabel.Should().Be("Episode");
            subject.ItemNoun.Should().Be("Episode");
            subject.ParentId.Should().Be(1);
            subject.ParentTitle.Should().Be("Game of Thrones");
            subject.Year.Should().Be(2011);
            subject.OmitYearFromTitle.Should().BeTrue();
            subject.ParentPath.Should().Be(_series.Path);
            subject.Rating.Should().Be(9.2m);
            subject.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
            subject.PosterUrl.Should().Be("https://img.example/poster.jpg");
            subject.FanartUrl.Should().Be("https://img.example/fanart.jpg");
            subject.GetExternalId(ExternalIdTypes.Tvdb).Should().Be("121361");
            subject.GetExternalId(ExternalIdTypes.Tmdb).Should().Be("1399");
            subject.GetExternalId(ExternalIdTypes.Imdb).Should().Be("tt0944947");
            subject.GetExternalId(ExternalIdTypes.TvMaze).Should().Be("82");
        }

        [Test]
        public void should_link_the_title_to_tvdb_and_offer_links_in_display_order()
        {
            var subject = Subject.Describe(_series, _episodes);

            subject.MetadataUrl.Should().Be("https://www.thetvdb.com/?tab=series&id=121361");
            subject.Links.Select(l => l.Label).Should().Equal("TVDb", "TMDb", "Trakt", "TVMaze", "IMDb");
            subject.GetLink(MetadataLinkType.Tvdb).Link.Should().Be("https://www.thetvdb.com/?tab=series&id=121361");
            subject.GetLink(MetadataLinkType.Tmdb).Link.Should().Be("https://www.themoviedb.org/tv/1399");
            subject.GetLink(MetadataLinkType.Trakt).Link.Should().Be("https://trakt.tv/search/tvdb/121361?id_type=show");
            subject.GetLink(MetadataLinkType.Tvmaze).Link.Should().Be("https://www.tvmaze.com/shows/82/_");
            subject.GetLink(MetadataLinkType.Imdb).Link.Should().Be("https://www.imdb.com/title/tt0944947");
        }

        [Test]
        public void should_skip_links_for_ids_the_series_does_not_have()
        {
            _series.TvdbId = 0;
            _series.TmdbId = 0;
            _series.TvMazeId = 0;
            _series.ImdbId = null;

            var subject = Subject.Describe(_series, _episodes);

            subject.Links.Should().BeEmpty();
            subject.ExternalIds.Should().BeEmpty();
            subject.MetadataUrl.Should().BeNull();
        }

        [Test]
        public void should_label_episodes_the_way_sonarr_titles_them()
        {
            Subject.Describe(_series, _episodes.Take(1).ToList()).ItemLabel.Should().Be("1x01 - Winter Is Coming");
            Subject.Describe(_series, _episodes).ItemLabel.Should().Be("1x01x02 - Winter Is Coming + The Kingsroad");
        }

        [Test]
        public void should_label_a_daily_episode_by_its_air_date()
        {
            _series.SeriesType = SeriesTypes.Daily;

            Subject.Describe(_series, _episodes).ItemLabel.Should().Be("2011-04-17 - Winter Is Coming");
        }

        [Test]
        public void should_have_no_item_label_without_episodes()
        {
            Subject.Describe(_series).ItemLabel.Should().BeEmpty();
            Subject.Describe(_series, new List<Episode>()).ItemLabel.Should().BeEmpty();
        }

        [Test]
        public void should_not_look_up_the_root_folder_until_it_is_read()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>()))
                .Returns("/tv");

            var subject = Subject.Describe(_series, _episodes);

            Mocker.GetMock<IRootFolderService>().Verify(s => s.GetBestRootFolderPath(It.IsAny<string>()), Times.Never());

            subject.RootFolderPath.Should().Be("/tv");

            Mocker.GetMock<IRootFolderService>().Verify(s => s.GetBestRootFolderPath(It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_describe_a_file_with_the_series_folder_as_its_path()
        {
            var file = TvGoldenData.EpisodeFile(_series, _episodes);

            var described = Subject.DescribeFile(_series, file);

            described.Id.Should().Be(10);
            described.RelativePath.Should().Be(file.RelativePath);
            described.Path.Should().Be(System.IO.Path.Combine(_series.Path, file.RelativePath));
            described.QualityName.Should().Be("WEBDL-1080p");
            described.MediaInfo.AudioLanguages.Should().Equal("eng", "fra");
            described.MediaInfo.Subtitles.Should().Equal("eng", "spa");
            Subject.DescribeFile(_series, null).Should().BeNull();
        }

        [Test]
        public void should_describe_a_grabbed_release()
        {
            var release = Subject.DescribeRelease(TvGoldenData.Grabbed());

            release.Title.Should().Be("Game.of.Thrones.S01E01E02.1080p.WEB-DL-GRP");
            release.Indexer.Should().Be("Knaben");
            release.Size.Should().Be(4294967296);
            Subject.DescribeRelease((NzbDrone.Core.Tv.Parser.Model.GrabbedReleaseInfo)null).Should().BeNull();
        }
    }
}
