using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Blocklisting;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    // BlocklistRepository stands in for every MediaKindRepository: rows written by
    // another module share the table and must stay invisible and untouched.
    [TestFixture]
    public class MediaKindRepositoryFixture : DbTest<BlocklistRepository, Blocklist>
    {
        private const string Title = "Shared.Title.2014.1080p";

        private Movie _movie;

        [SetUp]
        public void Setup()
        {
            _movie = Db.Insert(Builder<Movie>.CreateNew().BuildNew());

            Db.Insert(Row(MediaKind.Movies));
            Db.Insert(Row(MediaKind.Tv));
        }

        private Blocklist Row(MediaKind kind)
        {
            return new Blocklist
            {
                MediaKind = kind,
                MovieId = _movie.Id,
                SourceTitle = Title,
                TorrentInfoHash = "abc",
                Quality = new QualityModel(),
                Languages = new List<Language>(),
                Date = DateTime.UtcNow
            };
        }

        [Test]
        public void should_only_read_rows_of_its_kind()
        {
            Subject.All().Should().ContainSingle().Which.MediaKind.Should().Be(MediaKind.Movies);
            Subject.Count().Should().Be(1);
            Subject.BlocklistedByTitle(_movie.Id, Title).Should().ContainSingle();
            Subject.BlocklistedByTorrentInfoHash(_movie.Id, "abc").Should().ContainSingle();
            Subject.BlocklistedByMovie(_movie.Id).Should().ContainSingle();
        }

        [Test]
        public void should_not_get_a_row_of_another_kind_by_id()
        {
            var tvRow = AllStoredModels.Single(r => r.MediaKind == MediaKind.Tv);

            Subject.Find(tvRow.Id).Should().BeNull();
        }

        [Test]
        public void should_page_and_count_rows_of_its_kind_only()
        {
            var page = Subject.GetPaged(new PagingSpec<Blocklist> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending });

            page.TotalRecords.Should().Be(1);
            page.Records.Should().ContainSingle().Which.MediaKind.Should().Be(MediaKind.Movies);
        }

        [Test]
        public void should_not_delete_rows_of_another_kind()
        {
            Subject.DeleteForMovies(new List<int> { _movie.Id });

            AllStoredModels.Should().ContainSingle().Which.MediaKind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_purge_rows_of_its_kind_only()
        {
            Subject.Purge();

            AllStoredModels.Should().ContainSingle().Which.MediaKind.Should().Be(MediaKind.Tv);
        }

        [TestCase(MediaKind.Tv)]
        [TestCase((MediaKind)0)]
        public void should_refuse_to_insert_a_row_of_another_kind(MediaKind kind)
        {
            Assert.Throws<InvalidOperationException>(() => Subject.Insert(Row(kind)));
            Assert.Throws<InvalidOperationException>(() => Subject.InsertMany(new List<Blocklist> { Row(kind) }));

            AllStoredModels.Should().HaveCount(2);
        }
    }
}
