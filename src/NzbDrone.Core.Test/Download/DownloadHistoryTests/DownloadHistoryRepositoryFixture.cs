using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.DownloadHistoryTests
{
    [TestFixture]
    public class DownloadHistoryRepositoryFixture : DbTest<DownloadHistoryRepository, DownloadHistory>
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
        public void should_delete_history_items_by_parent_id()
        {
            var items = Builder<DownloadHistory>.CreateListOfSize(5)
                .All()
                .With(c => c.MediaKind = MediaKind.Movies)
                .TheFirst(1)
                .With(c => c.Id = 0)
                .With(c => c.ParentId = _movie2.Id)
                .TheRest()
                .With(c => c.Id = 0)
                .With(c => c.ParentId = _movie1.Id)
                .BuildListOfNew();

            Db.InsertMany(items);

            Subject.DeleteByParentIds(MediaKind.Movies, new List<int> { _movie1.Id });

            var removedItems = Subject.All().Where(h => h.ParentId == _movie1.Id);
            var nonRemovedItems = Subject.All().Where(h => h.ParentId == _movie2.Id);

            removedItems.Should().HaveCount(0);
            nonRemovedItems.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_history_of_another_media_type_with_the_same_parent_id()
        {
            var movie = Builder<DownloadHistory>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(c => c.ParentId = 7)
                .Build();

            var tv = Builder<DownloadHistory>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MediaKind = MediaKind.Tv)
                .With(c => c.ParentId = 7)
                .Build();

            Db.InsertMany(new List<DownloadHistory> { movie, tv });

            Subject.DeleteByParentIds(MediaKind.Movies, new List<int> { 7 });

            var remaining = Subject.All();

            remaining.Should().ContainSingle();
            remaining.Single().MediaKind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_find_a_download_of_any_media_type_by_download_id()
        {
            var movie = Builder<DownloadHistory>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MediaKind = MediaKind.Movies)
                .With(c => c.DownloadId = "A")
                .Build();

            var tv = Builder<DownloadHistory>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MediaKind = MediaKind.Tv)
                .With(c => c.DownloadId = "B")
                .Build();

            Db.InsertMany(new List<DownloadHistory> { movie, tv });

            Subject.FindByDownloadId("A").Should().ContainSingle(h => h.MediaKind == MediaKind.Movies);
            Subject.FindByDownloadId("B").Should().ContainSingle(h => h.MediaKind == MediaKind.Tv);
        }

        [Test]
        public void should_refuse_a_row_with_no_media_kind()
        {
            var item = Builder<DownloadHistory>.CreateNew()
                .With(c => c.Id = 0)
                .With(c => c.MediaKind = 0)
                .Build();

            Action insert = () => Subject.Insert(item);

            insert.Should().Throw<InvalidOperationException>();
        }
    }
}
