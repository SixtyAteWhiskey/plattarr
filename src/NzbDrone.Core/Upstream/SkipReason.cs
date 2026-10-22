namespace NzbDrone.Core.Upstream
{
    // Reason codes of the import report (docs/importer.md, "Report"). The codes
    // are part of the report format, so they never change once released.
    public static class SkipReason
    {
        public const string UnknownProvider = "UNKNOWN_PROVIDER";
        public const string UnmatchedIndexer = "UNMATCHED_INDEXER";
        public const string Duplicate = "DUPLICATE";
        public const string NoMediaType = "NO_MEDIA_TYPE";
        public const string UnknownEvent = "UNKNOWN_EVENT";
        public const string UnknownKey = "UNKNOWN_KEY";
        public const string ConfigConflict = "CONFIG_CONFLICT";
        public const string Instance = "INSTANCE";
        public const string Transient = "TRANSIENT";
        public const string Stats = "STATS";
        public const string Obsolete = "OBSOLETE";
        public const string Corrupt = "CORRUPT";
    }
}
