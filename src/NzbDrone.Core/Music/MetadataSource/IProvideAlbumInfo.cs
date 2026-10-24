using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Music.MetadataSource
{
    public interface IProvideAlbumInfo
    {
        Tuple<string, Album, List<ArtistMetadata>> GetAlbumInfo(string id);
        HashSet<string> GetChangedAlbums(DateTime startTime);
    }
}
