using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Blocklisting;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedBlocklistFixture : DbTest<CleanupOrphanedBlocklist, Blocklist>
    {
        [Test]
        public void should_delete_orphaned_blocklist_items()
        {
            var blocklist = Builder<Blocklist>.CreateNew()
                                              .With(c => c.MediaKind = MediaKind.Movies)
                                              .With(h => h.ParentId = default)
                                              .With(h => h.Quality = new QualityModel())
                                              .With(h => h.Languages = new List<Language>())
                                              .BuildNew();

            Db.Insert(blocklist);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_unorphaned_blocklist_items()
        {
            var movie = Builder<Movie>.CreateNew().BuildNew();

            Db.Insert(movie);

            var blocklist = Builder<Blocklist>.CreateNew()
                                              .With(c => c.MediaKind = MediaKind.Movies)
                                              .With(h => h.ParentId = default)
                                              .With(h => h.Quality = new QualityModel())
                                              .With(h => h.Languages = new List<Language>())
                                              .With(b => b.ParentId = movie.Id)
                                              .BuildNew();

            Db.Insert(blocklist);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_rows_of_other_media_types()
        {
            // A TV row's MovieId points at Tv_ tables, never at Movies_Movies.
            var blocklist = Builder<Blocklist>.CreateNew()
                                              .With(c => c.MediaKind = MediaKind.Tv)
                                              .With(h => h.ParentId = default)
                                              .With(h => h.Quality = new QualityModel())
                                              .With(h => h.Languages = new List<Language>())
                                              .BuildNew();

            Db.Insert(blocklist);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }
    }
}
