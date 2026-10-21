using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Download.Clients.DownloadStation
{
    // Decides whether a Download Station task is one of ours and which category it belongs to. The
    // client has no labels, so it goes by the task's destination folder: the configured directory,
    // or a category folder in the path.
    internal static class DownloadStationCategories
    {
        public static bool TryGetCategory(DownloadStationSettings settings, OsPath outputPath, out string category)
        {
            var categories = settings.MonitoredCategories();

            category = null;

            if (settings.TvDirectory.IsNotNullOrWhiteSpace())
            {
                if (!new OsPath($"/{settings.TvDirectory}").Contains(outputPath))
                {
                    return false;
                }
            }
            else if (categories.Any())
            {
                var directories = outputPath.FullPath.Split('\\', '/');

                category = categories.FirstOrDefault(c => directories.Contains(c));

                return category != null;
            }

            // The directory does not say which media type this is: it belongs to Movies, as ever.
            category = settings.ForCategory(null).Category;

            return true;
        }
    }
}
