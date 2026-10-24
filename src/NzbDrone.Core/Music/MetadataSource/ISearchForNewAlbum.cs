using System.Collections.Generic;

namespace NzbDrone.Core.Music.MetadataSource
{
    public interface ISearchForNewAlbum
    {
        List<Album> SearchForNewAlbum(string title, string artist);
        List<Album> SearchForNewAlbumByRecordingIds(List<string> recordingIds);
    }
}
