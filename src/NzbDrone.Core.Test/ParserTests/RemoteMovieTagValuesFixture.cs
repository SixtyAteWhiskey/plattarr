using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class RemoteMovieTagValuesFixture
    {
        [Test]
        public void should_expose_the_movie_values_download_clients_tag_with()
        {
            var remoteMovie = new RemoteMovie
            {
                ParsedMovieInfo = new ParsedMovieInfo { ReleaseGroup = "GRP" },
                Movie = new Movie
                {
                    MovieMetadata = new MovieMetadata { Year = 1999, CollectionTitle = "The Collection", Studio = "Studio One" }
                }
            };

            remoteMovie.ReleaseGroup.Should().Be("GRP");
            remoteMovie.Year.Should().Be(1999);
            remoteMovie.Collection.Should().Be("The Collection");
            remoteMovie.Studio.Should().Be("Studio One");
        }

        [Test]
        public void should_return_nothing_for_a_release_that_matched_no_movie()
        {
            var remoteMovie = new RemoteMovie();

            remoteMovie.ReleaseGroup.Should().BeNull();
            remoteMovie.Year.Should().BeNull();
            remoteMovie.Collection.Should().BeNull();
            remoteMovie.Studio.Should().BeNull();
        }
    }
}
