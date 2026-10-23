using System;

namespace NzbDrone.Core.Tv.Search
{
    public class DailyEpisodeSearchRequest : TvSearchRequest
    {
        public DateTime AirDate { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : {1:yyyy-MM-dd}]", Series.Title, AirDate);
        }
    }
}
