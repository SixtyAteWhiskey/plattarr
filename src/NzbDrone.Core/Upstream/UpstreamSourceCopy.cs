using System;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;

namespace NzbDrone.Core.Upstream
{
    // A private, read-only copy of one upstream database (docs/importer.md,
    // "Safety rules", item 2). The importer never opens a live upstream file: it
    // copies the .db and its -wal and -shm files (or extracts radarr.db from a
    // backup zip) into a scratch directory, and opens the copy read-only. Disposing
    // removes the scratch directory.
    //
    // A backup zip is the safer input while the upstream container runs: copying a
    // live database file can catch a write half done, a zip cannot.
    public sealed class UpstreamSourceCopy : IDisposable
    {
        private readonly string _workDirectory;

        private UpstreamSourceCopy(string originalPath, string workDirectory, string copyPath, string sha256)
        {
            OriginalPath = originalPath;
            _workDirectory = workDirectory;
            CopyPath = copyPath;
            Sha256 = sha256;
        }

        public string OriginalPath { get; }
        public string CopyPath { get; }

        // Hash of the copied database file, lower-case hex, for the report.
        public string Sha256 { get; }

        public static UpstreamSourceCopy Create(string sourcePath, string databaseFileName, string scratchDirectory)
        {
            if (!File.Exists(sourcePath))
            {
                throw new UpstreamImportException($"Source file not found: {sourcePath}");
            }

            var workDirectory = Path.Combine(scratchDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(workDirectory);

            try
            {
                var copyPath = Path.Combine(workDirectory, databaseFileName);

                if (string.Equals(Path.GetExtension(sourcePath), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    ExtractFromZip(sourcePath, databaseFileName, copyPath);
                }
                else
                {
                    CopyWithJournal(sourcePath, copyPath);
                }

                return new UpstreamSourceCopy(sourcePath, workDirectory, copyPath, HashFile(copyPath));
            }
            catch
            {
                DeleteDirectory(workDirectory);
                throw;
            }
        }

        // Opens the copy read-only, without pooling so the file is released on dispose.
        public IDbConnection OpenReadOnly()
        {
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = CopyPath,
                ReadOnly = true,
                FailIfMissing = true,
                Pooling = false
            };

            var connection = new SQLiteConnection(builder.ConnectionString);

            try
            {
                connection.Open();
            }
            catch
            {
                connection.Dispose();
                throw;
            }

            return connection;
        }

        public void Dispose()
        {
            SQLiteConnection.ClearAllPools();
            DeleteDirectory(_workDirectory);
        }

        private static void CopyWithJournal(string sourcePath, string copyPath)
        {
            File.Copy(sourcePath, copyPath);

            // A WAL database keeps recent writes in these two files; leaving them
            // out would silently drop those rows from the copy.
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                if (File.Exists(sourcePath + suffix))
                {
                    File.Copy(sourcePath + suffix, copyPath + suffix);
                }
            }
        }

        private static void ExtractFromZip(string zipPath, string databaseFileName, string copyPath)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            // Only the entry's name is used, never its stored path, so a crafted
            // zip cannot write outside the scratch directory.
            foreach (var entry in archive.Entries)
            {
                if (string.Equals(entry.Name, databaseFileName, StringComparison.OrdinalIgnoreCase))
                {
                    entry.ExtractToFile(copyPath);
                    return;
                }
            }

            throw new UpstreamImportException($"The backup {Path.GetFileName(zipPath)} does not contain {databaseFileName}");
        }

        private static string HashFile(string path)
        {
            using var stream = File.OpenRead(path);

            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }

        // A leftover scratch directory is not worth failing an import over.
        private static void DeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
