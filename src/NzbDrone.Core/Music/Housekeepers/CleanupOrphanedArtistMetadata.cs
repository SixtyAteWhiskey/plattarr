using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Music.Housekeepers
{
    // Lidarr's CleanupOrphanedArtistMetadata on the Music tables: delete the metadata rows that no artist, album or track uses.
    public class CleanupOrphanedArtistMetadata : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedArtistMetadata(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            mapper.Execute(@"DELETE FROM ""Music_ArtistMetadata""
                             WHERE ""Id"" IN (
                             SELECT ""Music_ArtistMetadata"".""Id"" FROM ""Music_ArtistMetadata""
                             LEFT OUTER JOIN ""Music_Albums"" ON ""Music_Albums"".""ArtistMetadataId"" = ""Music_ArtistMetadata"".""Id""
                             LEFT OUTER JOIN ""Music_Tracks"" ON ""Music_Tracks"".""ArtistMetadataId"" = ""Music_ArtistMetadata"".""Id""
                             LEFT OUTER JOIN ""Music_Artists"" ON ""Music_Artists"".""ArtistMetadataId"" = ""Music_ArtistMetadata"".""Id""
                             WHERE ""Music_Albums"".""Id"" IS NULL AND ""Music_Tracks"".""Id"" IS NULL AND ""Music_Artists"".""Id"" IS NULL)");
        }
    }
}
