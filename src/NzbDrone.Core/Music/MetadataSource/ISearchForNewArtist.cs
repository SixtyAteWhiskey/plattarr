using System.Collections.Generic;

namespace NzbDrone.Core.Music.MetadataSource
{
    public interface ISearchForNewArtist
    {
        List<Artist> SearchForNewArtist(string title);
    }
}
