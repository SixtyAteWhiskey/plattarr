using System.Collections.Generic;

namespace NzbDrone.Core.Tags
{
    // Every table with a Tags column (a JSON array of tag IDs). One list, two readers:
    // CleanupUnusedTags keeps a tag only while a row here uses it, and the upstream
    // importer remaps the same column in the same tables. A test compares the list
    // with the mapped entities, so a new tag-using table cannot be forgotten.
    //
    // AutoTagging also holds tag IDs inside its TagSpecification values; that is not
    // a Tags column and is handled separately by both readers.
    public static class TagUsage
    {
        public static readonly IReadOnlyList<string> Tables = new[]
        {
            "Movies_Movies",
            "Notifications",
            "DelayProfiles",
            "ReleaseProfiles",
            "Movies_ImportLists",
            "Indexers",
            "AutoTagging",
            "DownloadClients",
            "IndexerProxies",

            // Collections hand their tags to the movies they add (upstream Radarr leaves them out).
            "Movies_Collections"
        };
    }
}
