using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Music.Housekeepers
{
    // Lidarr's CleanupOrphanedTracks on the Music tables: delete the tracks whose release is gone.
    public class CleanupOrphanedTracks : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedTracks(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Music_Tracks""
                             WHERE ""Id"" IN (
                             SELECT ""Music_Tracks"".""Id"" FROM ""Music_Tracks""
                             LEFT OUTER JOIN ""Music_AlbumReleases""
                             ON ""Music_Tracks"".""AlbumReleaseId"" = ""Music_AlbumReleases"".""Id""
                             WHERE ""Music_AlbumReleases"".""Id"" IS NULL)");
        }
    }
}
