using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class UpstreamSourceCopyFixture : CoreTest
    {
        private string _scratch;

        [SetUp]
        public void Setup()
        {
            _scratch = Path.Combine(TempFolder, "scratch");
            Directory.CreateDirectory(_scratch);
        }

        private static long CountMovies(IDbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Movies";

            return (long)command.ExecuteScalar();
        }

        private static string Hash(string path)
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        }

        private string CreateSourceDatabase(string fileName = "radarr.db")
        {
            var path = Path.Combine(TempFolder, fileName);

            UpstreamTestDatabase.Create(
                path,
                "CREATE TABLE Movies (Id INTEGER PRIMARY KEY, Title TEXT)",
                "INSERT INTO Movies (Id, Title) VALUES (1, 'Sintel (2010)')");

            return path;
        }

        [Test]
        public void should_copy_the_database_and_leave_the_original_untouched()
        {
            var source = CreateSourceDatabase();
            var before = Hash(source);

            using (var copy = UpstreamSourceCopy.Create(source, "radarr.db", _scratch))
            {
                using var connection = copy.OpenReadOnly();

                CountMovies(connection).Should().Be(1);
                copy.CopyPath.Should().NotBe(source);
                copy.Sha256.Should().Be(before);
            }

            Hash(source).Should().Be(before);
        }

        [Test]
        public void should_open_the_copy_read_only()
        {
            var source = CreateSourceDatabase();

            using var copy = UpstreamSourceCopy.Create(source, "radarr.db", _scratch);
            using var connection = copy.OpenReadOnly();

            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Movies";

            Action act = () => command.ExecuteNonQuery();

            act.Should().Throw<SQLiteException>();
            CountMovies(connection).Should().Be(1);
        }

        [Test]
        public void should_copy_the_write_ahead_log_so_recent_rows_are_not_lost()
        {
            var source = Path.Combine(TempFolder, "radarr.db");
            var builder = new SQLiteConnectionStringBuilder { DataSource = source, Pooling = false };

            // Auto-checkpoint off keeps the rows in the -wal file while this
            // connection (a running upstream app) stays open.
            using var live = new SQLiteConnection(builder.ConnectionString);
            live.Open();

            foreach (var sql in new[]
                     {
                         "PRAGMA journal_mode=WAL",
                         "PRAGMA wal_autocheckpoint=0",
                         "CREATE TABLE Movies (Id INTEGER PRIMARY KEY, Title TEXT)",
                         "INSERT INTO Movies (Id, Title) VALUES (1, 'Sintel (2010)')"
                     })
            {
                using var command = live.CreateCommand();
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }

            File.Exists(source + "-wal").Should().BeTrue("the test only means something if the rows are in the WAL");

            using var copy = UpstreamSourceCopy.Create(source, "radarr.db", _scratch);
            using var connection = copy.OpenReadOnly();

            CountMovies(connection).Should().Be(1);
        }

        [Test]
        public void should_extract_the_database_from_a_backup_zip()
        {
            var source = CreateSourceDatabase("extracted.db");
            var zipPath = Path.Combine(TempFolder, "radarr_backup_v6.2.0.1234_2026.10.21_05.00.00.zip");

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("config.xml");
                archive.CreateEntryFromFile(source, "radarr.db");
            }

            using var copy = UpstreamSourceCopy.Create(zipPath, "radarr.db", _scratch);
            using var connection = copy.OpenReadOnly();

            CountMovies(connection).Should().Be(1);
            copy.Sha256.Should().Be(Hash(source));
        }

        [Test]
        public void should_fail_when_the_backup_zip_has_no_such_database()
        {
            var zipPath = Path.Combine(TempFolder, "backup.zip");

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntry("config.xml");
            }

            Action act = () => UpstreamSourceCopy.Create(zipPath, "radarr.db", _scratch);

            act.Should().Throw<UpstreamImportException>().WithMessage("*radarr.db*");
            Directory.GetDirectories(_scratch).Should().BeEmpty("a failed copy cleans up after itself");
        }

        [Test]
        public void should_not_write_outside_the_scratch_directory_for_a_hostile_zip_entry()
        {
            var source = CreateSourceDatabase("extracted.db");
            var zipPath = Path.Combine(TempFolder, "hostile.zip");

            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(source, "../escape/radarr.db");
            }

            using var copy = UpstreamSourceCopy.Create(zipPath, "radarr.db", _scratch);

            Path.GetFullPath(copy.CopyPath).Should().StartWith(Path.GetFullPath(_scratch));
            Directory.Exists(Path.Combine(TempFolder, "escape")).Should().BeFalse();
        }

        [Test]
        public void should_fail_for_a_missing_source_file()
        {
            Action act = () => UpstreamSourceCopy.Create(Path.Combine(TempFolder, "nothing.db"), "radarr.db", _scratch);

            act.Should().Throw<UpstreamImportException>().WithMessage("*not found*");
        }

        [Test]
        public void should_remove_its_scratch_directory_on_dispose()
        {
            var source = CreateSourceDatabase();

            string copyPath;

            using (var copy = UpstreamSourceCopy.Create(source, "radarr.db", _scratch))
            {
                copyPath = copy.CopyPath;
                File.Exists(copyPath).Should().BeTrue();
            }

            File.Exists(copyPath).Should().BeFalse();
            Directory.GetDirectories(_scratch).Should().BeEmpty();
        }
    }
}
