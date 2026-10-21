using System;
using Equ;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.Clients
{
    // One media type's routing and priority settings on a download client (docs/module-interface.md,
    // "Download client settings"). A client has one row and a list of these, one per media type it
    // serves; the category is how a finished download is attributed to a media type.
    public class ModuleCategorySettings : MemberwiseEquatable<ModuleCategorySettings>
    {
        public MediaKind Kind { get; set; }
        public string Category { get; set; }
        public string ImportedCategory { get; set; }
        public int RecentPriority { get; set; }
        public int OlderPriority { get; set; }

        public static string DefaultCategory(MediaKind kind)
        {
            return kind switch
            {
                MediaKind.Movies => "plattarr-movies",
                MediaKind.Tv => "plattarr-tv",
                MediaKind.Music => "plattarr-music",
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        public static ModuleCategorySettings CreateDefault(MediaKind kind, int priority)
        {
            return new ModuleCategorySettings
            {
                Kind = kind,
                Category = DefaultCategory(kind),
                RecentPriority = priority,
                OlderPriority = priority
            };
        }
    }
}
