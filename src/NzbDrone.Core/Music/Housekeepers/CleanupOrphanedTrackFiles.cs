using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Housekeeping;

namespace NzbDrone.Core.Music.Housekeepers
{
    // Lidarr's CleanupOrphanedTrackFiles on the Music tables. A file no track points at is unlinked from its album,
    // and a track whose file is gone is unlinked from it. The rows themselves stay: a file on disk is not deleted by housekeeping.
    public class CleanupOrphanedTrackFiles : IHousekeepingTask
    {
        private readonly IMainDatabase _database;

        public CleanupOrphanedTrackFiles(IMainDatabase database)
        {
            _database = database;
        }

        public void Clean()
        {
            CleanupOrphanedByAlbum();
            CleanupOrphanedByTracks();
        }

        private void CleanupOrphanedByAlbum()
        {
            using var mapper = _database.OpenConnection();

            // Unlink where track no longer exists
            mapper.Execute(@"UPDATE ""Music_TrackFiles""
                             SET ""AlbumId"" = 0
                             WHERE ""AlbumId"" <> 0 AND ""Id"" IN (
                             SELECT ""Music_TrackFiles"".""Id"" FROM ""Music_TrackFiles""
                             LEFT OUTER JOIN ""Music_Tracks""
                             ON ""Music_TrackFiles"".""Id"" = ""Music_Tracks"".""TrackFileId""
                             WHERE ""Music_Tracks"".""Id"" IS NULL)");
        }

        private void CleanupOrphanedByTracks()
        {
            using var mapper = _database.OpenConnection();

            // Unlink Tracks where the Trackfiles entry no longer exists
            mapper.Execute(@"UPDATE ""Music_Tracks""
                             SET ""TrackFileId"" = 0
                             WHERE ""TrackFileId"" <> 0 AND ""Id"" IN (
                             SELECT ""Music_Tracks"".""Id"" FROM ""Music_Tracks""
                             LEFT OUTER JOIN ""Music_TrackFiles""
                             ON ""Music_Tracks"".""TrackFileId"" = ""Music_TrackFiles"".""Id""
                             WHERE ""Music_TrackFiles"".""Id"" IS NULL)");
        }
    }
}
