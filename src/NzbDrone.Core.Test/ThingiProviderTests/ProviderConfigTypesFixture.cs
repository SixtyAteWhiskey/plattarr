using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.Indexers.Torznab;
using NzbDrone.Core.ThingiProvider;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.ThingiProviderTests
{
    // Finding 12 in the plan: settings are stored by short class name, and two provider
    // families can use the same name. The legacy indexer layer that first hit this is gone
    // (porting step 7); the duplicate cases below use test-local types.
    [TestFixture]
    public class ProviderConfigTypesFixture : TestBase
    {
        private static readonly Type[] Duplicates = { typeof(Alpha.SharedSettings), typeof(Beta.SharedSettings) };

        [TestCase("NewznabSettings", typeof(NewznabSettings))]
        [TestCase("TorznabSettings", typeof(TorznabSettings))]
        [TestCase("newznabsettings", typeof(NewznabSettings))]
        public void should_resolve_indexer_settings(string contract, Type expected)
        {
            ProviderConfigTypes.Find(contract, typeof(IndexerDefinition)).Should().Be(expected);
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
            ProviderConfigTypes.Find(contract, typeof(IndexerDefinition)).Should().BeNull();
        }

        [Test]
        public void should_pick_duplicate_name_under_definition_namespace()
        {
            ProviderConfigTypes.Resolve("SharedSettings", typeof(Alpha.AlphaDefinition), Duplicates).Should().Be(typeof(Alpha.SharedSettings));
            ProviderConfigTypes.Resolve("SharedSettings", typeof(Beta.BetaDefinition), Duplicates).Should().Be(typeof(Beta.SharedSettings));
        }

        [Test]
        public void should_throw_when_duplicate_name_is_outside_definition_namespace()
        {
            Assert.Throws<InvalidOperationException>(() => ProviderConfigTypes.Resolve("SharedSettings", typeof(DownloadClientDefinition), Duplicates));
        }
    }
}
