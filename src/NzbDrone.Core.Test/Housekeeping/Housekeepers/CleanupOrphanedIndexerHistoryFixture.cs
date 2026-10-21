using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.IndexerHistory;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedIndexerHistoryFixture : DbTest<CleanupOrphanedIndexerHistory, IndexerHistoryItem>
    {
        private IndexerDefinition GivenIndexer()
        {
            return Db.Insert(new IndexerDefinition
            {
                Name = "Indexer",
                Implementation = "Newznab",
                ConfigContract = nameof(NewznabSettings),
                Settings = new NewznabSettings { BaseUrl = "https://indexer.example" },
                Added = DateTime.UtcNow
            });
        }

        private void GivenHistory(int indexerId)
        {
            Db.Insert(new IndexerHistoryItem { IndexerId = indexerId, Date = DateTime.UtcNow, EventType = IndexerHistoryEventType.IndexerRss, Data = new Dictionary<string, string>() });
        }

        [Test]
        public void should_delete_history_of_deleted_indexer()
        {
            GivenHistory(99);

            Subject.Clean();

            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_keep_history_of_existing_indexer()
        {
            var indexer = GivenIndexer();
            GivenHistory(indexer.Id);
            GivenHistory(99);

            Subject.Clean();

            AllStoredModels.Should().ContainSingle().Which.IndexerId.Should().Be(indexer.Id);
        }
    }

    [TestFixture]
    public class CleanupOrphanedIndexerOverridesFixture : DbTest<CleanupOrphanedIndexerOverrides, IndexerOverride>
    {
        [Test]
        public void should_delete_overrides_of_deleted_indexer_only()
        {
            var indexer = Db.Insert(new IndexerDefinition
            {
                Name = "Indexer",
                Implementation = "Newznab",
                ConfigContract = nameof(NewznabSettings),
                Settings = new NewznabSettings { BaseUrl = "https://indexer.example" },
                Added = DateTime.UtcNow
            });

            Db.Insert(new IndexerOverride { IndexerId = indexer.Id, MediaKind = MediaKind.Movies, RemoveYear = true });
            Db.Insert(new IndexerOverride { IndexerId = 99, MediaKind = MediaKind.Movies, RemoveYear = true });

            Subject.Clean();

            AllStoredModels.Should().ContainSingle().Which.IndexerId.Should().Be(indexer.Id);
        }
    }
}
