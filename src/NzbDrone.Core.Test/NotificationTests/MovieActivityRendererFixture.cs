using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Notifications;
using NzbDrone.Core.Movies.RootFolders;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class MovieActivityRendererFixture : CoreTest<MovieActivityRenderer>
    {
        [Test]
        public void should_return_null_for_a_missing_movie()
        {
            Subject.Describe((Movie)null).Should().BeNull();
        }

        [Test]
        public void should_describe_the_movie_as_a_subject()
        {
            var movie = GoldenData.Movie();

            var subject = Subject.Describe(movie);

            subject.Kind.Should().Be(MediaKind.Movies);
            subject.KindLabel.Should().Be("Movie");
            subject.ParentTitle.Should().Be("Interstellar");
            subject.ItemLabel.Should().BeEmpty();
            subject.Year.Should().Be(2014);
            subject.ParentPath.Should().Be(movie.Path);
            subject.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
            subject.PosterUrl.Should().Be("https://img.example/poster.jpg");
            subject.FanartUrl.Should().Be("https://img.example/fanart.jpg");
            subject.GetExternalId(ExternalIdTypes.Tmdb).Should().Be("157336");
            subject.GetExternalId(ExternalIdTypes.Imdb).Should().Be("tt0816692");
        }

        [Test]
        public void should_offer_links_in_display_order_with_typed_links_first()
        {
            var subject = Subject.Describe(GoldenData.Movie());

            subject.Links.Select(l => l.Label).Should().Equal("TMDb", "Trakt", "IMDb", "YouTube", "Website");
            subject.GetLink(MetadataLinkType.Tmdb).Link.Should().Be("https://www.themoviedb.org/movie/157336");
            subject.GetLink(MetadataLinkType.Imdb).Link.Should().Be("https://www.imdb.com/title/tt0816692");
            subject.GetLink(MetadataLinkType.Trakt).Link.Should().Be("https://trakt.tv/movies/tt0816692");
        }

        [Test]
        public void should_skip_links_for_ids_the_movie_does_not_have()
        {
            var movie = GoldenData.Movie();
            movie.MovieMetadata.Value.TmdbId = 0;
            movie.MovieMetadata.Value.ImdbId = null;
            movie.MovieMetadata.Value.YouTubeTrailerId = null;
            movie.MovieMetadata.Value.Website = null;

            var subject = Subject.Describe(movie);

            subject.Links.Should().BeEmpty();
            subject.ExternalIds.Should().BeEmpty();
        }

        [Test]
        public void should_not_look_up_the_root_folder_until_it_is_read()
        {
            Mocker.GetMock<IRootFolderService>()
                .Setup(s => s.GetBestRootFolderPath(It.IsAny<string>(), null))
                .Returns("/movies");

            var subject = Subject.Describe(GoldenData.Movie());

            Mocker.GetMock<IRootFolderService>()
                .Verify(s => s.GetBestRootFolderPath(It.IsAny<string>(), null), Times.Never());

            subject.RootFolderPath.Should().Be("/movies");

            Mocker.GetMock<IRootFolderService>()
                .Verify(s => s.GetBestRootFolderPath(It.IsAny<string>(), null), Times.Once());
        }

        [Test]
        public void should_describe_a_file_with_the_movie_folder_as_its_path()
        {
            var movie = GoldenData.Movie();
            var file = GoldenData.MovieFile(movie);

            var described = Subject.DescribeFile(movie, file);

            described.Id.Should().Be(file.Id);
            described.RelativePath.Should().Be(file.RelativePath);
            described.Path.Should().Be(System.IO.Path.Combine(movie.Path, file.RelativePath));
            described.QualityName.Should().Be("Bluray-1080p");
            described.MediaInfo.AudioLanguages.Should().Equal("eng", "fra");
            Subject.DescribeFile(movie, null).Should().BeNull();
        }
    }
}
