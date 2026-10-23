using System;
using System.Collections.Generic;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Tv.MetadataSource
{
    public interface IProvideSeriesInfo
    {
        Tuple<Series, List<Episode>> GetSeriesInfo(int tvdbSeriesId, Language language, string seasonType);
    }
}
