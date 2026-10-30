using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.Indexers.Definitions.Cardigann;
using NzbDrone.Core.Indexers.Definitions.Gazelle;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerTests
{
    public class IndexerServiceFixture : DbTest<IndexerFactory, IndexerDefinition>
    {
        private List<IIndexer> _indexers;

        [SetUp]
        public void Setup()
        {
            _indexers = new List<IIndexer>();

            _indexers.Add(Mocker.Resolve<Newznab>());

            Mocker.SetConstant<IEnumerable<IIndexer>>(_indexers);
        }

        [Test]
        public void should_remove_missing_indexers_on_startup()
        {
            var repo = Mocker.Resolve<IndexerRepository>();

            Mocker.SetConstant<IIndexerRepository>(repo);

            var existingIndexers = Builder<IndexerDefinition>.CreateNew().BuildNew();
            existingIndexers.ConfigContract = nameof(NewznabSettings);

            repo.Insert(existingIndexers);

            Subject.Handle(new ApplicationStartedEvent());

            AllStoredModels.Should().NotContain(c => c.Id == existingIndexers.Id);

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_keep_a_stored_obsolete_native_indexer_on_startup()
        {
            var repo = Mocker.Resolve<IndexerRepository>();

            Mocker.SetConstant<IIndexerRepository>(repo);

#pragma warning disable CS0618 // Existing native rows must remain usable until their settings have a proven migration.
            _indexers.Add(Mocker.Resolve<CGPeers>());
#pragma warning restore CS0618

            var existingIndexer = repo.Insert(new IndexerDefinition
            {
                Name = "Legacy CGPeers",
                Implementation = "CGPeers",
                Settings = new GazelleSettings()
            });

            Subject.Handle(new ApplicationStartedEvent());

            AllStoredModels.Should().ContainSingle(c => c.Id == existingIndexer.Id);
        }

        [Test]
        public void should_round_trip_converted_funfile_settings_in_a_stored_row()
        {
            var repo = Mocker.Resolve<IndexerRepository>();
            const string nativeSettings = """
                {"baseUrl":"https://www.funfile.org/","username":"sample-user","password":"FAKE-PASSWORD","torrentBaseSettings":{"minimumSeeders":4}}
                """;
            var converter = new ProviderSettingConverter();
            var converted = (CardigannSettings)converter.Parse(FunFileSettingsConverter.Convert(nativeSettings), typeof(CardigannSettings));

            var row = repo.Insert(new IndexerDefinition
            {
                Name = "FunFile conversion proof",
                Implementation = "Cardigann",
                Settings = converted
            });

            var stored = (CardigannSettings)repo.Get(row.Id).Settings;
            stored.DefinitionFile.Should().Be("funfile");
            stored.ExtraFieldData["username"].Should().Be("sample-user");
            stored.ExtraFieldData["password"].Should().Be("FAKE-PASSWORD");
            stored.TorrentBaseSettings.MinimumSeeders.Should().Be(4);
        }
    }
}
