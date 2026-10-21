using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class IndexerSearchCheckFixture : CoreTest<IndexerSearchCheck>
    {
        private Mock<IIndexer> _indexerMock;
        private IndexerDefinition _definition;
        private bool _blocked;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses the fixture instance across tests.
            _indexerMock = null;
            _definition = null;
            _blocked = false;

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.GetAvailableProviders())
                  .Returns(new List<IIndexer>());

            // RssEnabled, AutomaticSearchEnabled, and InteractiveSearchEnabled are extension
            // methods over Enabled(); the indexer's definition flags decide what they return.
            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.Enabled(It.IsAny<bool>()))
                  .Returns<bool>(filterBlocked => _indexerMock == null || (filterBlocked && _blocked)
                      ? new List<IIndexer>()
                      : new List<IIndexer> { _indexerMock.Object });

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("Some Warning Message");
        }

        private void GivenIndexer(bool supportsRss, bool supportsSearch)
        {
            _definition = new IndexerDefinition
            {
                EnableRss = false,
                EnableAutomaticSearch = false,
                EnableInteractiveSearch = false
            };

            _indexerMock = Mocker.GetMock<IIndexer>();
            _indexerMock.SetupGet(s => s.Definition).Returns(_definition);
            _indexerMock.SetupGet(s => s.SupportsRss).Returns(supportsRss);
            _indexerMock.SetupGet(s => s.SupportsSearch).Returns(supportsSearch);

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(s => s.GetAvailableProviders())
                  .Returns(new List<IIndexer> { _indexerMock.Object });
        }

        private void GivenAutomaticSearchEnabled()
        {
            _definition.EnableAutomaticSearch = true;
        }

        private void GivenInteractiveSearchEnabled()
        {
            _definition.EnableInteractiveSearch = true;
        }

        private void GivenSearchFiltered()
        {
            _definition.EnableAutomaticSearch = true;
            _definition.EnableInteractiveSearch = true;
            _blocked = true;

            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("recent indexer errors");
        }

        [Test]
        public void should_return_warning_when_no_indexer_present()
        {
            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_warning_when_no_search_supported_indexer_present()
        {
            GivenIndexer(true, false);

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_ok_when_automatic_and__search_is_enabled()
        {
            GivenIndexer(false, true);
            GivenAutomaticSearchEnabled();
            GivenInteractiveSearchEnabled();

            Subject.Check().ShouldBeOk();
        }

        [Test]
        public void should_return_warning_when_only_automatic_search_is_enabled()
        {
            GivenIndexer(false, true);
            GivenAutomaticSearchEnabled();

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_warning_when_only_interactive_search_is_enabled()
        {
            GivenIndexer(false, true);
            GivenInteractiveSearchEnabled();

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_warning_if_search_is_supported_but_disabled()
        {
            GivenIndexer(false, true);

            Subject.Check().ShouldBeWarning();
        }

        [Test]
        public void should_return_filter_warning_if_search_is_enabled_but_filtered()
        {
            GivenIndexer(false, true);
            GivenSearchFiltered();

            Subject.Check().ShouldBeWarning("recent indexer errors");
        }
    }
}
