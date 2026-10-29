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
        public bool DeleteFilesOnFailure { get; set; }
        public bool SearchForReplacement { get; set; } = true;
        public int StrikeLimit { get; set; }
        public int StrikeIntervalMinutes { get; set; }
        public int MinimumDownloadRateKiBps { get; set; }
    }
}
