using System;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.Clients
{
    // The form field (and validation property) names for one media type's category settings. They
    // are the upstream wire names: Radarr's movieCategory, Sonarr's tvCategory, Lidarr's
    // musicCategory, and so on, so the compatibility APIs can expose the matching subset as is.
    public static class ModuleCategoryFieldNames
    {
        public static string Tag(MediaKind kind)
        {
            return kind switch
            {
                MediaKind.Movies => "Movie",
                MediaKind.Tv => "Tv",
                MediaKind.Music => "Music",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        public static string Category(MediaKind kind) => Tag(kind) + "Category";

        public static string ImportedCategory(MediaKind kind) => Tag(kind) + "ImportedCategory";

        public static string RecentPriority(MediaKind kind) => "Recent" + Tag(kind) + "Priority";

        public static string OlderPriority(MediaKind kind) => "Older" + Tag(kind) + "Priority";
    }
}
