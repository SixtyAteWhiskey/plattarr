using System.Collections.Generic;

namespace NzbDrone.Core.Music.MetadataSource
{
    public interface ISearchForNewEntity
    {
        List<object> SearchForNewEntity(string title);
    }
}
