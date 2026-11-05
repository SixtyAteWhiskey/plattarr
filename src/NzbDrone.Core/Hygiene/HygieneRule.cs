using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Hygiene
{
    // MediaKind and DownloadClientId are zero when a rule applies to all kinds/clients.
    // Enabled turns on monitoring and eligibility; cleanup execution has its own gate.
    public class HygieneRule : ModelBase
    {
        public string ReasonCode { get; set; }
        public int MediaKind { get; set; }
        public int DownloadClientId { get; set; }
        public bool Enabled { get; set; }
        public bool WarningCleanupEnabled { get; set; }
        public bool DeleteFilesOnFailure { get; set; }
        public bool SearchForReplacement { get; set; } = true;
        public int StrikeLimit { get; set; }
        public int StrikeIntervalMinutes { get; set; }
        public int MinimumDownloadRateKiBps { get; set; }

        // One literal value per line. Matches the download ID, category, tag,
        // tracker host, or configured download-client name.
        public string IgnoredDownloads { get; set; }

        // "all", "include", or "exclude"; patterns are case-insensitive substrings.
        public string FailedImportPatternMode { get; set; } = "all";
        public string FailedImportPatterns { get; set; }
        public int MinimumCompletionPercent { get; set; }
        public int MaximumCompletionPercent { get; set; } = 100;
        public long MinimumTorrentSizeBytes { get; set; }
        public long MaximumTorrentSizeBytes { get; set; }
        public int MaximumDownloadHours { get; set; }
        public bool IgnoreWhileAlternateSpeedActive { get; set; } = true;
    }
}
