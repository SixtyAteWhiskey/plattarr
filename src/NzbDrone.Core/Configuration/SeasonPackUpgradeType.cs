namespace NzbDrone.Core.Configuration
{
    // When a season pack replaces episodes on disk (TV): every episode must be an upgrade, a share
    // of them (SeasonPackUpgradeThreshold) must be, or any one is enough.
    public enum SeasonPackUpgradeType
    {
        All,
        Threshold,
        Any
    }
}
