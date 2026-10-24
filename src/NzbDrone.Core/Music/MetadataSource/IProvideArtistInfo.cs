using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Music.MetadataSource
{
    public interface IProvideArtistInfo
    {
        Artist GetArtistInfo(string lidarrId, int metadataProfileId);
        HashSet<string> GetChangedArtists(DateTime startTime);
    }
}
