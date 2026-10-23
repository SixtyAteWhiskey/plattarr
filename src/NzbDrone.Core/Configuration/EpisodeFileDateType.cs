namespace NzbDrone.Core.Configuration
{
    // TV: which air date, if any, is written to an imported episode file's modified time.
    public enum EpisodeFileDateType
    {
        None = 0,
        LocalAirDate = 1,
        UtcAirDate = 2
    }
}
