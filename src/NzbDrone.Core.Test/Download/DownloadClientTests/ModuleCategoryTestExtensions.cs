using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Modules;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.Test.Download.DownloadClientTests
{
    public static class ModuleCategoryTestExtensions
    {
        // The Movies entry of a client's settings: what the fixtures used to set as MovieCategory
        // and the movie priorities.
        public static ModuleCategorySettings Movies(this IProviderConfig settings)
        {
            return ((IModuleCategorySettings)settings).For(MediaKind.Movies);
        }

        // Gives the client an entry for TV, as the form does once a TV module is registered.
        public static ModuleCategorySettings AddTv(this IProviderConfig settings, string category, int recentPriority = 0, int olderPriority = 0)
        {
            var entry = new ModuleCategorySettings
            {
                Kind = MediaKind.Tv,
                Category = category,
                RecentPriority = recentPriority,
                OlderPriority = olderPriority
            };

            ((IModuleCategorySettings)settings).ModuleCategories.Add(entry);

            return entry;
        }

        public static ModuleCategorySettings Entry(this IProviderConfig settings, MediaKind kind)
        {
            return ((IModuleCategorySettings)settings).For(kind);
        }
    }
}
