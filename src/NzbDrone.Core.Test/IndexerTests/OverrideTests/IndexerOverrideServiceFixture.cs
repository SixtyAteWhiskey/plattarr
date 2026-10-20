using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.ThingiProvider.Events;

namespace NzbDrone.Core.Test.IndexerTests.OverrideTests
{
    [TestFixture]
    public class IndexerOverrideServiceFixture : DbTest<IndexerOverrideService, IndexerOverride>
    {
        [SetUp]
        public void Setup()
        {
            Mocker.SetConstant<IIndexerOverrideRepository>(Mocker.Resolve<IndexerOverrideRepository>());
        }

        [Test]
        public void should_return_empty_override_when_none_is_set()
        {
            var result = Subject.Get(5, MediaKind.Movies);

            result.IndexerId.Should().Be(5);
            result.MediaKind.Should().Be(MediaKind.Movies);
            result.MultiLanguages.Should().BeEmpty();
            result.RequiredFlags.Should().BeEmpty();
            result.Categories.Should().BeEmpty();
            result.RemoveYear.Should().BeNull();
        }

        [Test]
        public void should_round_trip_every_field()
        {
            Subject.Upsert(new IndexerOverride
            {
                IndexerId = 1,
                MediaKind = MediaKind.Movies,
                MultiLanguages = new List<int> { 1, 2 },
                RequiredFlags = new List<int> { 32 },
                RemoveYear = true,
                Categories = new List<int> { 2040, 100045 }
            });

            var stored = Db.All<IndexerOverride>().Single();

            stored.MultiLanguages.Should().Equal(1, 2);
            stored.RequiredFlags.Should().Equal(32);
            stored.RemoveYear.Should().BeTrue();
            stored.Categories.Should().Equal(2040, 100045);
        }

        [Test]
        public void should_replace_existing_row_for_same_indexer_and_media_type()
        {
            Subject.Upsert(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies, RemoveYear = true });
            Subject.Upsert(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies, RemoveYear = false });

            Db.All<IndexerOverride>().Should().ContainSingle().Which.RemoveYear.Should().BeFalse();
            Subject.Get(1, MediaKind.Movies).RemoveYear.Should().BeFalse();
        }

        [Test]
        public void should_keep_media_types_separate()
        {
            Subject.Upsert(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies, RemoveYear = true });
            Subject.Upsert(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Tv, RemoveYear = false });

            Db.All<IndexerOverride>().Should().HaveCount(2);
            Subject.Get(1, MediaKind.Movies).RemoveYear.Should().BeTrue();
        }

        [Test]
        public void should_delete_overrides_when_indexer_is_deleted()
        {
            Subject.Upsert(new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies, RemoveYear = true });
            Subject.Upsert(new IndexerOverride { IndexerId = 2, MediaKind = MediaKind.Movies, RemoveYear = true });

            Subject.Handle(new ProviderDeletedEvent<IIndexer>(1));

            Db.All<IndexerOverride>().Should().ContainSingle().Which.IndexerId.Should().Be(2);
            Subject.Get(1, MediaKind.Movies).RemoveYear.Should().BeNull();
        }
    }
}
