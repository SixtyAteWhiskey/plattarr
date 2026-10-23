using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.IndexerProxies;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.AutoTagging;
using NzbDrone.Core.Movies.ImportLists;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Tags
{
    [TestFixture]
    public class TagServiceFixture : CoreTest<TagService>
    {
        private Tag _tag;
        private Mock<ITaggedItemSource> _movies;
        private Mock<ITaggedItemSource> _series;

        [SetUp]
        public void Setup()
        {
            _tag = new Tag { Id = 7, Label = "kids" };

            Mocker.GetMock<ITagRepository>().Setup(v => v.Get(7)).Returns(_tag);
            Mocker.GetMock<ITagRepository>().Setup(v => v.All()).Returns(new List<Tag> { _tag });

            Mocker.GetMock<IDelayProfileService>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<DelayProfile>());
            Mocker.GetMock<IDelayProfileService>().Setup(v => v.All()).Returns(new List<DelayProfile>());
            Mocker.GetMock<IReleaseProfileService>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<ReleaseProfile>());
            Mocker.GetMock<IReleaseProfileService>().Setup(v => v.All()).Returns(new List<ReleaseProfile>());
            Mocker.GetMock<IImportListFactory>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<ImportListDefinition>());
            Mocker.GetMock<IImportListFactory>().Setup(v => v.All()).Returns(new List<ImportListDefinition>());
            Mocker.GetMock<INotificationFactory>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<NotificationDefinition>());
            Mocker.GetMock<INotificationFactory>().Setup(v => v.All()).Returns(new List<NotificationDefinition>());
            Mocker.GetMock<IIndexerFactory>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<IndexerDefinition>());
            Mocker.GetMock<IIndexerFactory>().Setup(v => v.All()).Returns(new List<IndexerDefinition>());
            Mocker.GetMock<IIndexerProxyFactory>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<IndexerProxyDefinition>());
            Mocker.GetMock<IIndexerProxyFactory>().Setup(v => v.All()).Returns(new List<IndexerProxyDefinition>());
            Mocker.GetMock<IDownloadClientFactory>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<DownloadClientDefinition>());
            Mocker.GetMock<IDownloadClientFactory>().Setup(v => v.All()).Returns(new List<DownloadClientDefinition>());
            Mocker.GetMock<IAutoTaggingService>().Setup(v => v.AllForTag(It.IsAny<int>())).Returns(new List<AutoTag>());
            Mocker.GetMock<IAutoTaggingService>().Setup(v => v.All()).Returns(new List<AutoTag>());

            _movies = GivenSource(MediaKind.Movies, new Dictionary<int, List<int>>());
            _series = GivenSource(MediaKind.Tv, new Dictionary<int, List<int>>());

            Mocker.SetConstant<IEnumerable<ITaggedItemSource>>(new[] { _movies.Object, _series.Object });
        }

        private static Mock<ITaggedItemSource> GivenSource(MediaKind kind, Dictionary<int, List<int>> itemTags)
        {
            var source = new Mock<ITaggedItemSource>();
            source.SetupGet(v => v.Kind).Returns(kind);
            source.Setup(v => v.AllItemTags()).Returns(itemTags);

            return source;
        }

        [Test]
        public void should_report_the_movies_and_series_that_carry_a_tag()
        {
            _movies.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 1, new List<int> { 7 } }, { 2, new List<int> { 8 } } });
            _series.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 5, new List<int> { 7, 8 } }, { 6, new List<int>() } });

            var details = Subject.Details(7);

            details.MovieIds.Should().Equal(1);
            details.SeriesIds.Should().Equal(5);
            details.InUse.Should().BeTrue();
        }

        [Test]
        public void should_report_the_movies_and_series_that_carry_each_tag()
        {
            _movies.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } });
            _series.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 5, new List<int> { 7 } } });

            var details = Subject.Details().Single();

            details.MovieIds.Should().Equal(1);
            details.SeriesIds.Should().Equal(5);
        }

        [Test]
        public void should_not_delete_a_tag_that_only_a_series_uses()
        {
            _series.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 5, new List<int> { 7 } } });

            Assert.Throws<ModelConflictException>(() => Subject.Delete(7));

            Mocker.GetMock<ITagRepository>().Verify(v => v.Delete(7), Times.Never());
        }

        [Test]
        public void should_not_delete_a_tag_that_only_a_movie_uses()
        {
            _movies.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 1, new List<int> { 7 } } });

            Assert.Throws<ModelConflictException>(() => Subject.Delete(7));

            Mocker.GetMock<ITagRepository>().Verify(v => v.Delete(7), Times.Never());
        }

        [Test]
        public void should_delete_a_tag_that_nothing_uses()
        {
            Subject.Delete(7);

            Mocker.GetMock<ITagRepository>().Verify(v => v.Delete(7), Times.Once());
        }

        [Test]
        public void should_not_count_items_of_another_kind_twice()
        {
            _movies.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 3, new List<int> { 7 } } });
            _series.Setup(v => v.AllItemTags()).Returns(new Dictionary<int, List<int>> { { 3, new List<int> { 7 } } });

            var details = Subject.Details(7);

            details.MovieIds.Should().Equal(3);
            details.SeriesIds.Should().Equal(3);
        }
    }
}
