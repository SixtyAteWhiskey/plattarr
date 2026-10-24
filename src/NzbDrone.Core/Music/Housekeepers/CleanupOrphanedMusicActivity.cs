using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.Housekeepers
{
    // The Music rows of the shared activity tables (History, Blocklist, PendingReleases) point at an artist,
    // and history also at an album. Delete the ones whose artist or album is gone (Lidarr's
    // CleanupOrphanedHistoryItems, CleanupOrphanedBlacklist and CleanupOrphanedPendingReleases). Rows of
    // other media types point at their own tables and are left to their module.
    public class CleanupOrphanedMusicActivity : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedMusicActivity(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();

            foreach (var table in new[] { "History", "Blocklist", "PendingReleases" })
            {
                mapper.Execute($@"DELETE FROM ""{table}""
                                 WHERE ""Id"" IN (
                                 SELECT ""{table}"".""Id"" FROM ""{table}""
                                 LEFT OUTER JOIN ""Music_Artists""
                                 ON ""{table}"".""ParentId"" = ""Music_Artists"".""Id""
                                 WHERE ""{table}"".""MediaKind"" = {(int)MediaKind.Music}
                                 AND ""Music_Artists"".""Id"" IS NULL)");
            }

            // A history row for an album that no longer exists.
            mapper.Execute($@"DELETE FROM ""History""
                             WHERE ""Id"" IN (
                             SELECT ""History"".""Id"" FROM ""History""
                             LEFT OUTER JOIN ""Music_Albums""
                             ON ""History"".""ItemId"" = ""Music_Albums"".""Id""
                             WHERE ""History"".""MediaKind"" = {(int)MediaKind.Music}
                             AND ""Music_Albums"".""Id"" IS NULL)");
        }
    }
}
