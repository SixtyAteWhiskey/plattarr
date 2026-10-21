using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedHistoryItemsFixture : DbTest<CleanupOrphanedHistoryItems, MovieHistory>
    {
        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = Builder<Movie>.CreateNew()
                                   .BuildNew();
        }

        private void GivenSeries()
        {
            Db.Insert(_movie);
        }

        [Test]
        public void should_delete_orphaned_items()
        {
            var history = Builder<MovieHistory>.CreateNew()
                                                  .With(c => c.MediaKind = MediaKind.Movies)
                                                  .With(h => h.Quality = new QualityModel())
                                                  .With(h => h.Languages = new List<Language>())
                                                  .BuildNew();
            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_unorphaned()
        {
            GivenSeries();

            var history = Builder<MovieHistory>.CreateNew()
                                                  .With(c => c.MediaKind = MediaKind.Movies)
                                                  .With(h => h.Quality = new QualityModel())
                                                  .With(h => h.Languages = new List<Language>())
                                                  .With(h => h.MovieId = _movie.Id)
                                                  .BuildNew();
            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_rows_of_other_media_types()
        {
            // A TV row's MovieId points at Tv_ tables, never at Movies_Movies.
            var history = Builder<MovieHistory>.CreateNew()
                                                  .With(c => c.MediaKind = MediaKind.Tv)
                                                  .With(h => h.Quality = new QualityModel())
                                                  .With(h => h.Languages = new List<Language>())
                                                  .BuildNew();
            Db.Insert(history);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }
    }
}
