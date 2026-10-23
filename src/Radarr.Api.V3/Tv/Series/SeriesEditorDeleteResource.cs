using System.Collections.Generic;

namespace Radarr.Api.V3.Tv.Series
{
    public class SeriesEditorDeleteResource
    {
        public List<int> SeriesIds { get; set; }
        public bool DeleteFiles { get; set; }
    }
}
