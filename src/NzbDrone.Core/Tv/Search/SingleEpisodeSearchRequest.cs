namespace NzbDrone.Core.Tv.Search
{
    public class SingleEpisodeSearchRequest : TvSearchRequest
    {
        public int EpisodeNumber { get; set; }
        public int SeasonNumber { get; set; }

        public override string ToString()
        {
            return string.Format("[{0} : S{1:00}E{2:00}]", Series.Title, SeasonNumber, EpisodeNumber);
        }
    }
}
