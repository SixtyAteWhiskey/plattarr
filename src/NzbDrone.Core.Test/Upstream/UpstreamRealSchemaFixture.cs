using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The schemas in Files/Upstream were dumped from the owner's live databases
    // (Radarr VersionInfo 242, Prowlarr 44; docs/importer.md, "Dump findings") and
    // contain no rows. Building an empty database from one gives the adapter the
    // true table and column set without any private data.
    [TestFixture]
    public class UpstreamRealSchemaFixture : CoreTest
    {
        private string _scratch;

        [SetUp]
        public void Setup()
        {
            _scratch = Path.Combine(TempFolder, "scratch");
            Directory.CreateDirectory(_scratch);
        }

        private SourceSnapshot ReadSchema(IUpstreamAdapter adapter, string schemaFile, long version, out HashSet<string> upstreamTables, params string[] extraStatements)
        {
            var source = Path.Combine(TempFolder, adapter.DatabaseFileName);

            UpstreamTestDatabase.CreateFromSchema(
                source,
                GetTestPath("Files/Upstream/" + schemaFile),
                new[] { $"INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES ({version}, '2026-10-21 00:00:00', 'test')" }
                    .Concat(extraStatements).ToArray());

            using var copy = UpstreamSourceCopy.Create(source, adapter.DatabaseFileName, _scratch);
            using var connection = copy.OpenReadOnly();

            upstreamTables = ListTables(connection);

            return adapter.Read(connection, new ImportOptions());
        }

        private static HashSet<string> ListTables(IDbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name <> 'sqlite_sequence'";

            using var reader = command.ExecuteReader();
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (reader.Read())
            {
                tables.Add(reader.GetString(0));
            }

            return tables;
        }

        private static IEnumerable<string> Accounted(SourceSnapshot snapshot)
        {
            return snapshot.Tables.Where(t => t.Exists).Select(t => t.Name)
                .Concat(snapshot.SkippedTables.Select(t => t.Name));
        }

        [Test]
        public void should_read_or_count_every_table_in_the_real_radarr_schema()
        {
            var snapshot = ReadSchema(new RadarrAdapter(), "radarr-242.schema.sql", 242, out var upstreamTables);

            upstreamTables.Should().HaveCount(41, "the dump found 41 tables in radarr.db");
            Accounted(snapshot).Should().BeEquivalentTo(upstreamTables);
            Accounted(snapshot).Should().OnlyHaveUniqueItems("a table is read or counted, never both");
        }

        [Test]
        public void should_read_or_count_every_table_in_the_real_prowlarr_schema()
        {
            var snapshot = ReadSchema(new ProwlarrAdapter(), "prowlarr-44.schema.sql", 44, out var upstreamTables);

            upstreamTables.Should().HaveCount(20, "the dump found 20 tables in prowlarr.db");
            Accounted(snapshot).Should().BeEquivalentTo(upstreamTables);
            Accounted(snapshot).Should().OnlyHaveUniqueItems("a table is read or counted, never both");
        }

        [Test]
        public void should_never_read_the_tables_that_hold_credentials_or_instance_identity()
        {
            var radarr = ReadSchema(new RadarrAdapter(), "radarr-242.schema.sql", 242, out _);

            radarr.Table("Users").Exists.Should().BeFalse();

            var prowlarr = ReadSchema(new ProwlarrAdapter(), "prowlarr-44.schema.sql", 44, out _);

            prowlarr.Table("Users").Exists.Should().BeFalse();
            prowlarr.Table("Config").Exists.Should().BeFalse();
        }

        [Test]
        public void should_read_a_radarr_torznab_indexer_row_with_its_nullable_flags()
        {
            // Scrubbed shape from the dump: baseUrl ends in the Prowlarr indexer ID,
            // EnableRss and EnableAutomaticSearch are nullable columns.
            var settings = "{\"baseUrl\":\"http://prowlarr.invalid:9696/10/\",\"apiPath\":\"/api\",\"apiKey\":\"00000000000000000000000000000000\",\"minimumSeeders\":1,\"multiLanguages\":[],\"requiredFlags\":[],\"categories\":[2000,2010]}";

            var snapshot = ReadSchema(
                new RadarrAdapter(),
                "radarr-242.schema.sql",
                242,
                out _,
                $"INSERT INTO Indexers (Id, Name, Implementation, Settings, ConfigContract, EnableRss, EnableAutomaticSearch, EnableInteractiveSearch, Priority, Tags, DownloadClientId) VALUES (29, 'Example (Prowlarr)', 'Torznab', '{settings}', 'TorznabSettings', NULL, 0, 1, 25, '[]', 0)");

            var indexer = snapshot.Rows("Indexers").Should().ContainSingle().Subject;

            indexer.GetInt("Id").Should().Be(29);
            indexer.GetString("ConfigContract").Should().Be("TorznabSettings");
            indexer.GetNullableInt("EnableRss").Should().BeNull();
            indexer.GetBool("EnableAutomaticSearch").Should().BeFalse();
            indexer.GetBool("EnableInteractiveSearch").Should().BeTrue();
            indexer.GetJson("Settings")["baseUrl"].Value<string>().Should().EndWith("/10/");
            indexer.GetJson("Tags").HasValues.Should().BeFalse();
        }

        [Test]
        public void should_read_a_prowlarr_indexer_row_without_the_optional_seed_settings()
        {
            // The owner's rows set only preferMagnetUrl in torrentBaseSettings
            // (docs/importer.md, "Dump findings"): appMinimumSeeders and the seed
            // settings are absent, and the read layer must not need them.
            var settings = "{\"definitionFile\":\"example\",\"extraFieldData\":{},\"baseSettings\":{\"limitsUnit\":0},\"torrentBaseSettings\":{\"preferMagnetUrl\":false}}";

            var snapshot = ReadSchema(
                new ProwlarrAdapter(),
                "prowlarr-44.schema.sql",
                44,
                out _,
                $"INSERT INTO Indexers (Id, Name, Implementation, Settings, ConfigContract, Enable, Priority, Added, Redirect, AppProfileId, Tags, DownloadClientId) VALUES (10, 'Example', 'Cardigann', '{settings}', 'CardigannSettings', 1, 25, '2026-01-01 00:00:00', 0, 1, '[]', 0)");

            var indexer = snapshot.Rows("Indexers").Should().ContainSingle().Subject;
            var torrent = indexer.GetJson("Settings")["torrentBaseSettings"];

            torrent["preferMagnetUrl"].Value<bool>().Should().BeFalse();
            (torrent["appMinimumSeeders"] == null).Should().BeTrue();
            indexer.GetInt("AppProfileId").Should().Be(1);
        }
    }
}
