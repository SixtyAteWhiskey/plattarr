using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Music.Housekeepers
{
    // Lidarr's CleanupOrphanedAlbums on the Music tables: delete the albums whose artist is gone.
    public class CleanupOrphanedAlbums : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedAlbums(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Music_Albums""
                             WHERE ""Id"" IN (
                             SELECT ""Music_Albums"".""Id"" FROM ""Music_Albums""
                             LEFT OUTER JOIN ""Music_Artists""
                             ON ""Music_Albums"".""ArtistMetadataId"" = ""Music_Artists"".""ArtistMetadataId""
                             WHERE ""Music_Artists"".""Id"" IS NULL)");
        }
    }
}
