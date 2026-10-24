using System.Collections.Generic;

namespace NzbDrone.Core.Music.MediaFiles.TrackImport.Identification
{
    public class CandidateAlbumRelease
    {
        public CandidateAlbumRelease()
        {
        }

        public CandidateAlbumRelease(AlbumRelease release)
        {
            AlbumRelease = release;
            ExistingTracks = new List<TrackFile>();
        }

        public AlbumRelease AlbumRelease { get; set; }
        public List<TrackFile> ExistingTracks { get; set; }
    }
}
