using System.Collections.Generic;
using NzbDrone.Core.Languages;

namespace NzbDrone.Core.Tv.MetadataSource
{
    public interface ISearchForNewSeries
    {
        List<Series> SearchForNewSeries(string title, Language language);
        List<Series> SearchForNewSeriesByImdbId(string imdbId, Language language);
        List<Series> SearchForNewSeriesByAniListId(int aniListId, Language language);
        List<Series> SearchForNewSeriesByTmdbId(int tmdbId, Language language);
        List<Series> SearchForNewSeriesByMyAnimeListId(int malId, Language language);
    }
}
