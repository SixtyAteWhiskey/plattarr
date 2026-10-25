using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // Every table an adapter reads must end up in a stage. While the stages are being built, the
    // tables no stage claims yet are listed here. The list can only shrink: a table a stage now
    // claims fails the test until it is removed, and a name that is not a table the adapter reads
    // fails it too, so the list cannot rot. When it is empty, delete it and assert on
    // "nothing unclaimed".
    [TestFixture]
    public class ImportStageCoverageFixture : CoreTest
    {
        private static readonly string[] RadarrNotYetClaimed =
        {
            "ReleaseProfiles",
            "DownloadClients", "RemotePathMappings", "Notifications", "AutoTagging",
            "CustomFilters", "NamingConfig", "Metadata", "ImportLists", "ImportListMovies",
            "History", "DownloadHistory", "Blocklist", "Config"
        };

        private static readonly string[] ProwlarrNotYetClaimed =
        {
            "DownloadClients", "Notifications", "CustomFilters"
        };

        // Tier 2 or not needed to run (docs/importer.md, "From sonarr.db"); Plattarr has no TV
        // import lists, so ImportLists and ImportListExclusions wait for them.
        private static readonly string[] SonarrNotYetClaimed =
        {
            "ReleaseProfiles",
            "DownloadClients", "RemotePathMappings", "Notifications", "AutoTagging",
            "CustomFilters", "NamingConfig", "Metadata", "ImportLists", "ImportListExclusions",
            "History", "DownloadHistory", "Blocklist", "Config"
        };

        // The stage classes, built with null dependencies: Tables and Order never use them.
        private static List<IImportStage> Stages()
        {
            return typeof(IImportStage).Assembly.ImplementationsOf<IImportStage>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t =>
                {
                    var constructor = t.GetConstructors().Single();
                    var arguments = constructor.GetParameters().Select(_ => (object)null).ToArray();

                    return (IImportStage)constructor.Invoke(arguments);
                })
                .ToList();
        }

        private SourceSnapshot RadarrSnapshot()
        {
            return UpstreamFixtureTest.ReadSnapshot(new RadarrAdapter(), GetTestPath("Files/Upstream/radarr-242.schema.sql"), 242, Array.Empty<string>(), TempFolder);
        }

        private SourceSnapshot ProwlarrSnapshot()
        {
            return UpstreamFixtureTest.ReadSnapshot(new ProwlarrAdapter(), GetTestPath("Files/Upstream/prowlarr-44.schema.sql"), 44, Array.Empty<string>(), TempFolder);
        }

        private SourceSnapshot SonarrSnapshot()
        {
            return UpstreamFixtureTest.ReadSnapshot(new SonarrAdapter(), GetTestPath("Files/Upstream/sonarr-217.schema.sql"), 217, Array.Empty<string>(), TempFolder);
        }

        private static List<string> Unclaimed(SourceSnapshot snapshot, IEnumerable<SourceTableRef> claimed)
        {
            var claimedHere = claimed.Where(c => c.App == snapshot.AppName).Select(c => c.Table).ToHashSet();

            return snapshot.Tables.Select(t => t.Name).Where(t => !claimedHere.Contains(t)).ToList();
        }

        [Test]
        public void radarr_tables_no_stage_claims_yet_should_be_exactly_the_listed_ones()
        {
            var claimed = Stages().SelectMany(s => s.Tables).ToList();

            Unclaimed(RadarrSnapshot(), claimed).Should().BeEquivalentTo(RadarrNotYetClaimed);
        }

        [Test]
        public void prowlarr_tables_no_stage_claims_yet_should_be_exactly_the_listed_ones()
        {
            var claimed = Stages().SelectMany(s => s.Tables).ToList();

            Unclaimed(ProwlarrSnapshot(), claimed).Should().BeEquivalentTo(ProwlarrNotYetClaimed);
        }

        [Test]
        public void sonarr_tables_no_stage_claims_yet_should_be_exactly_the_listed_ones()
        {
            var claimed = Stages().SelectMany(s => s.Tables).ToList();

            Unclaimed(SonarrSnapshot(), claimed).Should().BeEquivalentTo(SonarrNotYetClaimed);
        }

        [Test]
        public void stages_should_only_claim_tables_the_adapters_read()
        {
            var radarr = RadarrSnapshot().Tables.Select(t => new SourceTableRef(UpstreamApps.Radarr, t.Name));
            var prowlarr = ProwlarrSnapshot().Tables.Select(t => new SourceTableRef(UpstreamApps.Prowlarr, t.Name));
            var sonarr = SonarrSnapshot().Tables.Select(t => new SourceTableRef(UpstreamApps.Sonarr, t.Name));

            Stages().SelectMany(s => s.Tables).Should().BeSubsetOf(radarr.Concat(prowlarr).Concat(sonarr));
        }

        [Test]
        public void stages_should_not_claim_the_same_table_twice()
        {
            Stages().SelectMany(s => s.Tables).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void app_names_should_match_the_adapters()
        {
            UpstreamApps.Radarr.Should().Be(new RadarrAdapter().AppName);
            UpstreamApps.Prowlarr.Should().Be(new ProwlarrAdapter().AppName);
            UpstreamApps.Sonarr.Should().Be(new SonarrAdapter().AppName);
        }
    }
}
