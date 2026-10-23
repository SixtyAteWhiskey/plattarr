namespace NzbDrone.Core.Configuration
{
    // TV: whether an episode must have a title (not TBA) before its file is imported.
    public enum EpisodeTitleRequiredType
    {
        Always = 0,
        BulkSeasonReleases = 1,
        Never = 2
    }
}
