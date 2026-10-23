using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv
{
    public class TvTaggedItemSource : ITaggedItemSource
    {
        private readonly ISeriesService _seriesService;

        public TvTaggedItemSource(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public Dictionary<int, List<int>> AllItemTags()
        {
            return _seriesService.GetAllSeriesTags();
        }
    }
}
