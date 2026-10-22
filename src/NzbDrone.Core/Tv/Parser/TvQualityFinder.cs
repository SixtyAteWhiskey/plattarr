using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Instrumentation;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Tv.Parser
{
    // Sonarr's source vocabulary. The global catalog uses Radarr's (source plus
    // modifier); Sonarr's parser reasons in this one, so the TV module keeps it
    // private and translates at the catalog boundary (docs/catalogs.md). The
    // order is Sonarr's: the fallback search compares sources with >=.
    public enum TvQualitySource
    {
        Unknown,
        Television,
        TelevisionRaw,
        Web,
        WebRip,
        Dvd,
        Bluray,
        BlurayRaw
    }

    // Sonarr's QualityFinder over the qualities Sonarr defines, expressed with
    // catalog qualities. A quality the TV parser can return is in this table and
    // nothing else is.
    internal static class TvQualityFinder
    {
        private static readonly Logger Logger = NzbDroneLogger.GetLogger(typeof(TvQualityFinder));

        private static readonly (Quality Quality, TvQualitySource Source, int Resolution)[] Table =
        {
            (Quality.Unknown, TvQualitySource.Unknown, 0),
            (Quality.SDTV, TvQualitySource.Television, 480),
            (Quality.DVD, TvQualitySource.Dvd, 480),
            (Quality.WEBDL1080p, TvQualitySource.Web, 1080),
            (Quality.HDTV720p, TvQualitySource.Television, 720),
            (Quality.WEBDL720p, TvQualitySource.Web, 720),
            (Quality.Bluray720p, TvQualitySource.Bluray, 720),
            (Quality.Bluray1080p, TvQualitySource.Bluray, 1080),
            (Quality.WEBDL480p, TvQualitySource.Web, 480),
            (Quality.HDTV1080p, TvQualitySource.Television, 1080),
            (Quality.RAWHD, TvQualitySource.TelevisionRaw, 1080),
            (Quality.WEBRip480p, TvQualitySource.WebRip, 480),
            (Quality.Bluray480p, TvQualitySource.Bluray, 480),
            (Quality.WEBRip720p, TvQualitySource.WebRip, 720),
            (Quality.WEBRip1080p, TvQualitySource.WebRip, 1080),
            (Quality.HDTV2160p, TvQualitySource.Television, 2160),
            (Quality.WEBRip2160p, TvQualitySource.WebRip, 2160),
            (Quality.WEBDL2160p, TvQualitySource.Web, 2160),
            (Quality.Bluray2160p, TvQualitySource.Bluray, 2160),
            (Quality.Remux1080p, TvQualitySource.BlurayRaw, 1080),
            (Quality.Remux2160p, TvQualitySource.BlurayRaw, 2160),
            (Quality.Bluray576p, TvQualitySource.Bluray, 576),
        };

        public static IReadOnlyCollection<Quality> Qualities => Table.Select(t => t.Quality).ToList();

        // The source Sonarr gives a quality; Unknown for a catalog quality Sonarr
        // does not define (a file extension never maps to one).
        public static TvQualitySource SourceOf(Quality quality)
        {
            foreach (var entry in Table)
            {
                if (entry.Quality == quality)
                {
                    return entry.Source;
                }
            }

            return TvQualitySource.Unknown;
        }

        public static Quality FindBySourceAndResolution(TvQualitySource source, int resolution)
        {
            var matches = Table.Where(t => t.Source == source && t.Resolution == resolution).ToList();

            if (matches.Count == 1)
            {
                return matches[0].Quality;
            }

            // Handle 576p releases that have a Television or Web source, so they don't get rolled up to Bluray 576p
            if (resolution < 720)
            {
                switch (source)
                {
                    case TvQualitySource.Television:
                        return Quality.SDTV;
                    case TvQualitySource.Web:
                        return Quality.WEBDL480p;
                    case TvQualitySource.WebRip:
                        return Quality.WEBRip480p;
                }
            }

            var nearestQuality = Quality.Unknown;

            foreach (var entry in Table.Where(t => t.Resolution == resolution).OrderBy(t => t.Source))
            {
                if (entry.Source >= source)
                {
                    nearestQuality = entry.Quality;
                    break;
                }
            }

            Logger.Warn("Unable to find exact quality for {0} and {1}. Using {2} as fallback", source, resolution, nearestQuality);

            return nearestQuality;
        }
    }
}
