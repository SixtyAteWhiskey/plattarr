using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Test.Common;
using LegacyIndexerDefinition = NzbDrone.Core.Movies.Indexers.Legacy.IndexerDefinition;
using LegacyNewznabSettings = NzbDrone.Core.Movies.Indexers.Legacy.Newznab.NewznabSettings;
using LegacyTorznabSettings = NzbDrone.Core.Movies.Indexers.Legacy.Torznab.TorznabSettings;
using ProwlarrIndexerDefinition = NzbDrone.Core.Indexers.IndexerDefinition;
using ProwlarrNewznabSettings = NzbDrone.Core.Indexers.Newznab.NewznabSettings;
using ProwlarrTorznabSettings = NzbDrone.Core.Indexers.Torznab.TorznabSettings;

namespace NzbDrone.Core.Test.ThingiProviderTests
{
    [TestFixture]
    public class ProviderConfigTypesFixture : TestBase
    {
        [TestCase("NewznabSettings", typeof(LegacyNewznabSettings))]
        [TestCase("TorznabSettings", typeof(LegacyTorznabSettings))]
        [TestCase("newznabsettings", typeof(LegacyNewznabSettings))]
        public void should_resolve_legacy_settings_for_legacy_indexers(string contract, Type expected)
        {
            ProviderConfigTypes.Find(contract, typeof(LegacyIndexerDefinition)).Should().Be(expected);
        }

        [TestCase("NewznabSettings", typeof(ProwlarrNewznabSettings))]
        [TestCase("TorznabSettings", typeof(ProwlarrTorznabSettings))]
        public void should_resolve_prowlarr_settings_for_prowlarr_indexers(string contract, Type expected)
        {
            ProviderConfigTypes.Find(contract, typeof(ProwlarrIndexerDefinition)).Should().Be(expected);
        }

        [Test]
        public void should_resolve_unique_name_without_namespace_match()
        {
            ProviderConfigTypes.Find("QBittorrentSettings", typeof(DownloadClientDefinition)).Should().Be(typeof(QBittorrentSettings));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("NoSuchSettings")]
        public void should_return_null_for_unknown_contract(string contract)
        {
            ProviderConfigTypes.Find(contract, typeof(ProwlarrIndexerDefinition)).Should().BeNull();
        }

        [Test]
        public void should_throw_when_duplicate_name_is_outside_definition_namespace()
        {
            Assert.Throws<InvalidOperationException>(() => ProviderConfigTypes.Find("NewznabSettings", typeof(DownloadClientDefinition)));
        }
    }
}
