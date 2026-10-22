using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download.Clients
{
    // Settings of a download client that routes by category: one ModuleCategorySettings per media
    // type. The list is sparse. A client has an entry for every media type its form was saved with.
    public interface IModuleCategorySettings
    {
        List<ModuleCategorySettings> ModuleCategories { get; set; }
    }

    public static class ModuleCategorySettingsExtensions
    {
        // The entry a release of this kind is sent with. A missing entry is a configuration
        // error, not a reason to invent a category: the grab fails and says why.
        public static ModuleCategorySettings For(this IModuleCategorySettings settings, MediaKind kind)
        {
            var entry = settings.ModuleCategories?.FirstOrDefault(c => c.Kind == kind);

            if (entry == null)
            {
                throw new DownloadClientException("This download client has no category settings for {0}", kind);
            }

            return entry;
        }

        // The media type a download in this category belongs to: the one whose entry carries the
        // category. Null for an empty category or one no entry uses. Tracked download routing rule 2.
        public static MediaKind? KindForCategory(this IModuleCategorySettings settings, string category)
        {
            if (category.IsNullOrWhiteSpace())
            {
                return null;
            }

            return settings.ModuleCategories?
                .FirstOrDefault(c => c.Category.IsNotNullOrWhiteSpace() && string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase))?
                .Kind;
        }

        // The entry a download belongs to: the one whose category it sits in. A download that
        // matches no entry gets Movies' (the category lookup the clients use to read their own
        // items; tracked download routing does not use this fallback, see KindForCategory).
        public static ModuleCategorySettings ForCategory(this IModuleCategorySettings settings, string category)
        {
            var entry = category.IsNullOrWhiteSpace()
                ? null
                : settings.ModuleCategories?.FirstOrDefault(c => c.Category.IsNotNullOrWhiteSpace() && string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase));

            return entry ?? settings.For(MediaKind.Movies);
        }

        public static ModuleCategorySettings ForItem(this IModuleCategorySettings settings, DownloadClientItem item)
        {
            return settings.ForCategory(item.Category);
        }

        // Every non-empty category the client watches for finished and running downloads.
        public static List<string> MonitoredCategories(this IModuleCategorySettings settings)
        {
            return (settings.ModuleCategories ?? new List<ModuleCategorySettings>())
                .Select(c => c.Category)
                .Where(c => c.IsNotNullOrWhiteSpace())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // True when a download in this category is one of ours. With no category configured at
        // all the client takes everything, as a client with an empty category always did.
        public static bool Monitors(this IModuleCategorySettings settings, string category)
        {
            var monitored = settings.MonitoredCategories();

            return monitored.Empty() || monitored.Contains(category ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }
    }
}
