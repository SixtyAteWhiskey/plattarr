using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class PendingReleaseRepositoryFixture : DbTest<PendingReleaseRepository, PendingRelease>
    {
        private Movie _movie1;
        private Movie _movie2;

        [SetUp]
        public void Setup()
        {
            _movie1 = Builder<Movie>.CreateNew()
                                     .With(s => s.Id = 7)
                                     .Build();

            _movie2 = Builder<Movie>.CreateNew()
                                     .With(s => s.Id = 8)
                                     .Build();
        }

        [Test]
        public void should_delete_files_by_movieId()
        {
            var files = Builder<PendingRelease>.CreateListOfSize(5)
                .All()
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(c => c.Id = 0)
                .With(c => c.MovieId = _movie1.Id)
                .With(c => c.Release = new ReleaseInfo())
                .BuildListOfNew();

            Db.InsertMany(files);

            Subject.DeleteByMovieIds(new List<int> { _movie1.Id });

            var remainingFiles = Subject.AllByMovieId(_movie1.Id);

            remainingFiles.Should().HaveCount(0);
        }

        [Test]
        public void should_get_files_by_movieId()
        {
            var files = Builder<PendingRelease>.CreateListOfSize(5)
                .All()
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(c => c.Id = 0)
                .With(c => c.MovieId = _movie1.Id)
                .With(c => c.Release = new ReleaseInfo())
                .Random(2)
                .With(c => c.MovieId = _movie2.Id)
                .BuildListOfNew();

            Db.InsertMany(files);

            var remainingFiles = Subject.AllByMovieId(_movie1.Id);

            remainingFiles.Should().HaveCount(3);
            remainingFiles.Should().OnlyContain(c => c.MovieId == _movie1.Id);
        }

        [Test]
        public void should_round_trip_shared_release_model()
        {
            var release = new ReleaseInfo
            {
                Title = "Movie.2024.1080p.BluRay.x264-GROUP",
                Size = null,
                Languages = new List<string> { "German", "English" },
                IndexerFlags = new HashSet<IndexerFlag> { IndexerFlag.Internal }
            };

            release.AddIndexerFlags(IndexerFlags.PTP_Golden);

            var pending = Builder<PendingRelease>.CreateNew()
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(c => c.Id = 0)
                .With(c => c.MovieId = _movie1.Id)
                .With(c => c.Release = release)
                .BuildNew();

            Db.Insert(pending);

            var stored = Subject.AllByMovieId(_movie1.Id).Should().ContainSingle().Subject.Release;

            stored.Size.Should().BeNull();
            IndexerLanguageMapper.Map(stored.Languages).Should().Equal(Language.German, Language.English);
            stored.ToIndexerFlags().Should().Be(IndexerFlags.G_Internal | IndexerFlags.PTP_Golden);
        }
    }
}
