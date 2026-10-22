using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // These tests use synthetic schemas to pin the read layer's behavior. Whether
    // the table lists match the real Radarr and Prowlarr schemas is checked
    // separately, against the scrubbed real-shape fixtures.
    [TestFixture]
    public class UpstreamAdapterFixture : CoreTest
    {
        private const string VersionTable = "CREATE TABLE VersionInfo (Version INTEGER NOT NULL, AppliedOn DATETIME, Description TEXT)";

        private static string VersionRow(long version)
        {
            return $"INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES ({version}, '2026-10-21 00:00:00', 'test')";
        }

        private static string[] MinimalRadarr(long version, params string[] extra)
        {
            return new[]
            {
                VersionTable,
                VersionRow(version),
                "CREATE TABLE Movies (Id INTEGER PRIMARY KEY, Title TEXT, Path TEXT, Monitored INTEGER)",
                "INSERT INTO Movies (Id, Title, Path, Monitored) VALUES (1, 'Sintel (2010)', '/movies/Sintel (2010)', 1)",
                "CREATE TABLE MovieMetadata (Id INTEGER PRIMARY KEY, Title TEXT)",
                "INSERT INTO MovieMetadata (Id, Title) VALUES (1, 'Sintel')"
            }.Concat(extra).ToArray();
        }

        private SourceSnapshot Read(IUpstreamAdapter adapter, string[] statements, ImportOptions options = null)
        {
            var scratch = Path.Combine(TempFolder, "scratch");
            Directory.CreateDirectory(scratch);

            var source = Path.Combine(TempFolder, adapter.DatabaseFileName);
            UpstreamTestDatabase.Create(source, statements);

            using var copy = UpstreamSourceCopy.Create(source, adapter.DatabaseFileName, scratch);
            using var connection = copy.OpenReadOnly();

            return adapter.Read(connection, options ?? new ImportOptions());
        }

        [Test]
        public void should_report_the_supported_versions_found_in_the_owners_databases()
        {
            // docs/importer.md, "Dump findings": Radarr 242 and Prowlarr 44.
            new RadarrAdapter().SupportedVersions.Max.Should().Be(242);
            new ProwlarrAdapter().SupportedVersions.Max.Should().Be(44);
        }

        [Test]
        public void should_name_the_database_file_each_adapter_reads()
        {
            new RadarrAdapter().DatabaseFileName.Should().Be("radarr.db");
            new ProwlarrAdapter().DatabaseFileName.Should().Be("prowlarr.db");
        }

        [Test]
        public void should_read_rows_in_column_order_and_take_the_highest_version()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(241).Concat(new[] { VersionRow(242) }).ToArray());

            snapshot.AppName.Should().Be("Radarr");
            snapshot.Version.Should().Be(242);
            snapshot.Table("Movies").Columns.Should().Equal("Id", "Title", "Path", "Monitored");

            var movie = snapshot.Rows("Movies").Should().ContainSingle().Subject;
            movie.GetInt("Id").Should().Be(1);
            movie.GetString("Title").Should().Be("Sintel (2010)");
            movie.GetString("Path").Should().Be("/movies/Sintel (2010)");
            movie.GetBool("Monitored").Should().BeTrue();
        }

        [Test]
        public void should_return_an_empty_table_for_a_table_the_source_lacks()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(242));

            snapshot.Table("Movies").Exists.Should().BeTrue();
            snapshot.Table("MovieFiles").Exists.Should().BeFalse();
            snapshot.Rows("MovieFiles").Should().BeEmpty();
        }

        [Test]
        public void should_return_an_empty_table_for_a_table_that_is_not_in_the_adapters_list()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(242, "CREATE TABLE Unrelated (Id INTEGER)", "INSERT INTO Unrelated (Id) VALUES (1)"));

            snapshot.Rows("Unrelated").Should().BeEmpty();
        }

        [Test]
        public void should_read_a_row_whose_column_the_source_lacks()
        {
            var statements = new[]
            {
                VersionTable,
                VersionRow(242),
                "CREATE TABLE Movies (Id INTEGER PRIMARY KEY, Title TEXT)",
                "INSERT INTO Movies (Id, Title) VALUES (1, 'Sintel (2010)')",
                "CREATE TABLE MovieMetadata (Id INTEGER PRIMARY KEY)"
            };

            var movie = Read(new RadarrAdapter(), statements).Rows("Movies").Single();

            movie.Has("Path").Should().BeFalse();
            movie.GetString("Path").Should().BeNull();
            movie.GetString("Title").Should().Be("Sintel (2010)");
        }

        [Test]
        public void should_count_the_tables_it_does_not_read()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(
                242,
                "CREATE TABLE PendingReleases (Id INTEGER)",
                "INSERT INTO PendingReleases (Id) VALUES (1)",
                "INSERT INTO PendingReleases (Id) VALUES (2)",
                "CREATE TABLE Users (Id INTEGER)",
                "INSERT INTO Users (Id) VALUES (1)"));

            snapshot.Table("PendingReleases").Exists.Should().BeFalse("counted tables are never read");

            var pending = snapshot.SkippedTables.Single(t => t.Name == "PendingReleases");
            pending.RowCount.Should().Be(2);
            pending.Reason.Should().Be("TRANSIENT");

            var users = snapshot.SkippedTables.Single(t => t.Name == "Users");
            users.RowCount.Should().Be(1);
            users.Reason.Should().Be("INSTANCE");
        }

        [Test]
        public void should_not_list_a_counted_table_the_source_lacks()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(242));

            snapshot.SkippedTables.Select(t => t.Name).Should().NotContain("Commands");
        }

        [Test]
        public void should_refuse_a_source_newer_than_the_supported_range()
        {
            Action act = () => Read(new RadarrAdapter(), MinimalRadarr(243));

            act.Should().Throw<UpstreamImportException>()
                .Which.Message.Should().Contain("243").And.Contain("242").And.Contain("allowNewerSource");
        }

        [Test]
        public void should_read_a_newer_source_with_a_warning_when_allowed()
        {
            var snapshot = Read(new RadarrAdapter(), MinimalRadarr(243), new ImportOptions { AllowNewerSource = true });

            snapshot.Rows("Movies").Should().HaveCount(1);
            snapshot.Warnings.Should().ContainSingle().Which.Should().Contain("243");
        }

        [Test]
        public void should_refuse_a_source_older_than_the_supported_range_even_when_newer_is_allowed()
        {
            Action act = () => Read(new RadarrAdapter(), MinimalRadarr(0), new ImportOptions { AllowNewerSource = true });

            act.Should().Throw<UpstreamImportException>().Which.Message.Should().Contain("older");
        }

        [Test]
        public void should_refuse_a_database_with_no_recorded_version()
        {
            var statements = MinimalRadarr(242).Where(s => !s.StartsWith("INSERT INTO VersionInfo")).ToArray();

            Action act = () => Read(new RadarrAdapter(), statements);

            act.Should().Throw<UpstreamImportException>().Which.Message.Should().Contain("no schema version");
        }

        [Test]
        public void should_refuse_another_apps_database()
        {
            var prowlarrShaped = new[]
            {
                VersionTable,
                VersionRow(44),
                "CREATE TABLE Indexers (Id INTEGER PRIMARY KEY, Name TEXT)",
                "CREATE TABLE AppSyncProfiles (Id INTEGER PRIMARY KEY, Name TEXT)"
            };

            Action act = () => Read(new RadarrAdapter(), prowlarrShaped);

            act.Should().Throw<UpstreamImportException>().Which.Message.Should().Contain("does not look like a Radarr database");
        }

        [Test]
        public void should_refuse_a_file_that_is_not_a_database()
        {
            var scratch = Path.Combine(TempFolder, "scratch");
            Directory.CreateDirectory(scratch);

            var source = Path.Combine(TempFolder, "radarr.db");
            File.WriteAllText(source, "this is not a sqlite database, just a long enough line of text to look like a damaged file");

            using var copy = UpstreamSourceCopy.Create(source, "radarr.db", scratch);

            Action act = () =>
            {
                using var connection = copy.OpenReadOnly();
                new RadarrAdapter().Read(connection, new ImportOptions());
            };

            act.Should().Throw<UpstreamImportException>();
        }

        [Test]
        public void should_read_prowlarr_application_rows_but_only_count_its_config()
        {
            var statements = new[]
            {
                VersionTable,
                VersionRow(44),
                "CREATE TABLE Indexers (Id INTEGER PRIMARY KEY, Name TEXT, Settings TEXT)",
                "INSERT INTO Indexers (Id, Name, Settings) VALUES (10, 'Knaben', '{\"baseUrl\":\"https://example.invalid/\"}')",
                "CREATE TABLE AppSyncProfiles (Id INTEGER PRIMARY KEY, Name TEXT)",
                "INSERT INTO AppSyncProfiles (Id, Name) VALUES (1, 'Standard')",
                "CREATE TABLE Applications (Id INTEGER PRIMARY KEY, Name TEXT, Implementation TEXT)",
                "INSERT INTO Applications (Id, Name, Implementation) VALUES (1, 'Radarr', 'Radarr')",
                "CREATE TABLE Config (Id INTEGER PRIMARY KEY, Key TEXT, Value TEXT)",
                "INSERT INTO Config (Id, Key, Value) VALUES (1, 'apikey', 'not-a-real-secret')"
            };

            var snapshot = Read(new ProwlarrAdapter(), statements);

            snapshot.AppName.Should().Be("Prowlarr");
            snapshot.Rows("Indexers").Single().GetInt("Id").Should().Be(10);
            snapshot.Rows("Applications").Single().GetString("Implementation").Should().Be("Radarr");
            snapshot.Table("Config").Exists.Should().BeFalse("Config holds secrets and is never read");
            snapshot.SkippedTables.Single(t => t.Name == "Config").RowCount.Should().Be(1);
        }

        [Test]
        public void should_account_for_every_table_the_design_note_lists_for_radarr()
        {
            // Source tables named in docs/importer.md, "From radarr.db", plus Tags.
            var read = new[]
            {
                "Movies", "MovieMetadata", "MovieFiles", "AlternativeTitles", "Credits", "Collections",
                "ImportListMovies", "MovieTranslations", "MetadataFiles", "ExtraFiles", "SubtitleFiles",
                "RootFolders", "NamingConfig", "ImportLists", "ImportExclusions", "Metadata",
                "QualityProfiles", "CustomFormats", "QualityDefinitions", "DelayProfiles", "ReleaseProfiles",
                "AutoTagging", "CustomFilters", "RemotePathMappings", "DownloadClients", "Notifications",
                "Indexers", "History", "DownloadHistory", "Blocklist", "Config", "Tags"
            };

            var counted = new[]
            {
                "PendingReleases", "Users", "ScheduledTasks", "Commands", "Logs", "UpdateHistory", "VersionInfo",
                "IndexerStatus", "DownloadClientStatus", "NotificationStatus", "ImportListStatus"
            };

            var statements = read.Concat(counted.Where(t => t != "VersionInfo"))
                .Select(t => $"CREATE TABLE \"{t}\" (Id INTEGER)")
                .Concat(new[] { VersionTable, VersionRow(242) })
                .ToArray();

            var snapshot = Read(new RadarrAdapter(), statements);

            snapshot.Tables.Where(t => t.Exists).Select(t => t.Name).Should().BeEquivalentTo(read);
            snapshot.SkippedTables.Select(t => t.Name).Should().BeEquivalentTo(counted);
        }

        [Test]
        public void should_account_for_every_table_the_design_note_lists_for_prowlarr()
        {
            // docs/importer.md, "From prowlarr.db" and "Dump findings".
            var read = new[]
            {
                "Indexers", "AppSyncProfiles", "IndexerProxies", "Applications", "ApplicationIndexerMapping",
                "DownloadClients", "Notifications", "Tags", "CustomFilters"
            };

            var counted = new[]
            {
                "Config", "History", "Users", "VersionInfo", "IndexerStatus", "IndexerDefinitionVersions",
                "ApplicationStatus", "DownloadClientStatus", "NotificationStatus", "Commands", "ScheduledTasks",
                "UpdateHistory", "Logs"
            };

            var statements = read.Concat(counted.Where(t => t != "VersionInfo"))
                .Select(t => $"CREATE TABLE \"{t}\" (Id INTEGER)")
                .Concat(new[] { VersionTable, VersionRow(44) })
                .ToArray();

            var snapshot = Read(new ProwlarrAdapter(), statements);

            snapshot.Tables.Where(t => t.Exists).Select(t => t.Name).Should().BeEquivalentTo(read);
            snapshot.SkippedTables.Select(t => t.Name).Should().BeEquivalentTo(counted);
        }
    }
}
