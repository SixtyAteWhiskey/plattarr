using System;
using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.IndexerProxies.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.AutoTagging;
using NzbDrone.Core.Movies.AutoTagging.Specifications;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupUnusedTagsFixture : DbTest<CleanupUnusedTags, Tag>
    {
        private Mock<IAutoTagUsageSource> _tvAutoTags;

        [SetUp]
        public void Setup()
        {
            _tvAutoTags = new Mock<IAutoTagUsageSource>();
            _tvAutoTags.SetupGet(v => v.Kind).Returns(MediaKind.Tv);
            _tvAutoTags.Setup(v => v.All()).Returns(new List<AutoTagUsage>());
            Mocker.SetConstant<IEnumerable<IAutoTagUsageSource>>(new[] { _tvAutoTags.Object });
        }

        [Test]
        public void should_delete_unused_tags()
        {
            var tags = Builder<Tag>.CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();

            Db.InsertMany(tags);
            Subject.Clean();
            AllStoredModels.Should().BeEmpty();
        }

        [Test]
        public void should_not_delete_used_tags()
        {
            var tags = Builder<Tag>.CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            var restrictions = Builder<ReleaseProfile>.CreateListOfSize(2)
                .All()
                .With(v => v.Id = 0)
                .With(v => v.Tags.Add(tags[0].Id))
                .BuildList();
            Db.InsertMany(restrictions);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_tags_used_by_prowlarr_indexers_or_indexer_proxies()
        {
            var tags = Builder<Tag>.CreateListOfSize(3)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            Db.Insert(new IndexerDefinition
            {
                Name = "Indexer",
                Implementation = "Newznab",
                ConfigContract = nameof(NewznabSettings),
                Settings = new NewznabSettings { BaseUrl = "https://indexer.example" },
                Added = DateTime.UtcNow,
                Tags = new HashSet<int> { tags[0].Id }
            });

            Db.Insert(new IndexerProxyDefinition
            {
                Name = "Proxy",
                Implementation = "Http",
                ConfigContract = nameof(HttpSettings),
                Settings = new HttpSettings { Host = "proxy.example", Port = 8080 },
                Tags = new HashSet<int> { tags[1].Id }
            });

            Subject.Clean();

            AllStoredModels.Should().HaveCount(2);
            AllStoredModels.Should().NotContain(t => t.Id == tags[2].Id);
        }

        [Test]
        public void should_not_delete_tags_used_by_collections()
        {
            var tags = Builder<Tag>.CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            Db.Insert(new MovieCollection
            {
                Title = "Collection",
                CleanTitle = "collection",
                TmdbId = 10,
                RootFolderPath = "/movies",
                Images = new List<NzbDrone.Core.MediaCover.MediaCover>(),
                Added = DateTime.UtcNow,
                Tags = new HashSet<int> { tags[0].Id }
            });

            Subject.Clean();

            AllStoredModels.Should().ContainSingle(t => t.Id == tags[0].Id);
        }

        [Test]
        public void should_not_delete_used_auto_tagging_tag_specification_tags()
        {
            var tags = Builder<Tag>
                .CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            var autoTags = Builder<AutoTag>.CreateListOfSize(1)
                .All()
                .With(x => x.Id = 0)
                .With(x => x.Specifications = new List<IAutoTaggingSpecification>
                {
                    new TagSpecification
                    {
                        Name = "Test",
                        Value = tags[0].Id
                    }
                })
                .BuildList();

            Mocker.GetMock<IAutoTaggingRepository>().Setup(s => s.All())
                .Returns(autoTags);

            Subject.Clean();
            AllStoredModels.Should().HaveCount(1);
        }

        [Test]
        public void should_not_delete_tags_applied_by_a_series_auto_tag()
        {
            var tags = Builder<Tag>.CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            Db.Insert(new NzbDrone.Core.Tv.AutoTagging.AutoTag
            {
                Name = "Series",
                Tags = new HashSet<int> { tags[0].Id },
                Specifications = new List<NzbDrone.Core.Tv.AutoTagging.Specifications.IAutoTaggingSpecification>()
            });

            Subject.Clean();

            AllStoredModels.Should().ContainSingle(t => t.Id == tags[0].Id);
        }

        [Test]
        public void should_not_delete_tags_a_series_auto_tag_looks_for()
        {
            var tags = Builder<Tag>.CreateListOfSize(2)
                .All()
                .With(x => x.Id = 0)
                .BuildList();
            Db.InsertMany(tags);

            _tvAutoTags.Setup(v => v.All()).Returns(new List<AutoTagUsage>
            {
                new AutoTagUsage { Id = 1, ConditionTags = new List<int> { tags[1].Id } }
            });

            Subject.Clean();

            AllStoredModels.Should().ContainSingle(t => t.Id == tags[1].Id);
        }
    }
}
