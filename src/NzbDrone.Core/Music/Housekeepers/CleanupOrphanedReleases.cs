using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Music.Housekeepers
{
    // Lidarr's CleanupOrphanedReleases on the Music tables: delete the releases whose album is gone.
    public class CleanupOrphanedReleases : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedReleases(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Music_AlbumReleases""
                             WHERE ""Id"" IN (
                             SELECT ""Music_AlbumReleases"".""Id"" FROM ""Music_AlbumReleases""
                             LEFT OUTER JOIN ""Music_Albums""
                             ON ""Music_AlbumReleases"".""AlbumId"" = ""Music_Albums"".""Id""
                             WHERE ""Music_Albums"".""Id"" IS NULL)");
        }
    }
}
