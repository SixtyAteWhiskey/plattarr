namespace NzbDrone.Core.Tv.Search
{
    public class SeasonSearchRequest : TvSearchRequest
    {
        public int SeasonNumber { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : S{1:00}]", Series.Title, SeasonNumber);
        }
    }
}
