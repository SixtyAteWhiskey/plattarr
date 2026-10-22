namespace NzbDrone.Core.Movies.DecisionEngine
{
    public enum UpgradeableRejectReason
    {
        None,
        BetterQuality,
        BetterRevision,
        QualityCutoff,
        CustomFormatScore,
        CustomFormatCutoff,
        MinCustomFormatScore,
        UpgradesNotAllowed
    }
}
