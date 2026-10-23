using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.Housekeepers
{
    // The TV rows of the shared activity tables (History, Blocklist, PendingReleases) point at a series, and
    // history also at an episode. Delete the ones whose series or episode is gone. Rows of other media types
    // point at their own tables and are left to their module.
    public class CleanupOrphanedTvActivity : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedTvActivity(IMainDatabase database)
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
                                 LEFT OUTER JOIN ""Tv_Series""
                                 ON ""{table}"".""ParentId"" = ""Tv_Series"".""Id""
                                 WHERE ""{table}"".""MediaKind"" = {(int)MediaKind.Tv}
                                 AND ""Tv_Series"".""Id"" IS NULL)");
            }

            // A history row for an episode that no longer exists (Sonarr cleans these too).
            mapper.Execute($@"DELETE FROM ""History""
                             WHERE ""Id"" IN (
                             SELECT ""History"".""Id"" FROM ""History""
                             LEFT OUTER JOIN ""Tv_Episodes""
                             ON ""History"".""ItemId"" = ""Tv_Episodes"".""Id""
                             WHERE ""History"".""MediaKind"" = {(int)MediaKind.Tv}
                             AND ""Tv_Episodes"".""Id"" IS NULL)");
        }
    }
}
