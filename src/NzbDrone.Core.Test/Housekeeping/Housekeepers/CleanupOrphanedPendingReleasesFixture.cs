using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.Pending;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedPendingReleasesFixture : DbTest<CleanupOrphanedPendingReleases, PendingRelease>
    {
        [Test]
        public void should_delete_orphaned_pending_items()
        {
            var pendingRelease = Builder<PendingRelease>.CreateNew()
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(h => h.ParsedInfo = new ParsedMovieInfo())
                .With(h => h.Release = new ReleaseInfo())
                .BuildNew();

            Db.Insert(pendingRelease);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_unorphaned_pending_items()
        {
            var movie = Builder<Movie>.CreateNew().BuildNew();

            Db.Insert(movie);

            var pendingRelease = Builder<PendingRelease>.CreateNew()
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(h => h.ParentId = movie.Id)
                .With(h => h.ParsedInfo = new ParsedMovieInfo())
                .With(h => h.Release = new ReleaseInfo())
                .BuildNew();

            Db.Insert(pendingRelease);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_rows_of_other_media_types()
        {
            // A TV row's MovieId points at Tv_ tables, never at Movies_Movies.
            var pendingRelease = Builder<PendingRelease>.CreateNew()
                .With(c => c.MediaKind = MediaKind.Tv)
                .With(h => h.ParsedInfo = new ParsedMovieInfo())
                .With(h => h.Release = new ReleaseInfo())
                .BuildNew();

            Db.Insert(pendingRelease);
            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }
    }
}
