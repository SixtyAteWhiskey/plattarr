namespace NzbDrone.Core.Tv.Search
{
    public class DailySeasonSearchRequest : TvSearchRequest
    {
        public int Year { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : {1}]", Series.Title, Year);
        }
    }
}
