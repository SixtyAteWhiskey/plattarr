using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Qualities.Upstream
{
    // Two-way map between one upstream app's quality IDs and Plattarr's catalog
    // IDs (docs/catalogs.md). Used by the migration importer and the
    // compatibility APIs. Upstream IDs come from Radarr develop 14afe52.
    public static class RadarrQualityIds
    {
        private static readonly Dictionary<int, int> ToCatalogMap = new()
        {
            { 0, Quality.Unknown.Id },
            { 1, Quality.SDTV.Id },
            { 2, Quality.DVD.Id },
            { 3, Quality.WEBDL1080p.Id },
            { 4, Quality.HDTV720p.Id },
            { 5, Quality.WEBDL720p.Id },
            { 6, Quality.Bluray720p.Id },
            { 7, Quality.Bluray1080p.Id },
            { 8, Quality.WEBDL480p.Id },
            { 9, Quality.HDTV1080p.Id },
            { 10, Quality.RAWHD.Id },
            { 12, Quality.WEBRip480p.Id },
            { 14, Quality.WEBRip720p.Id },
            { 15, Quality.WEBRip1080p.Id },
            { 16, Quality.HDTV2160p.Id },
            { 17, Quality.WEBRip2160p.Id },
            { 18, Quality.WEBDL2160p.Id },
            { 19, Quality.Bluray2160p.Id },
            { 20, Quality.Bluray480p.Id },
            { 21, Quality.Bluray576p.Id },
            { 22, Quality.BRDISK.Id },
            { 23, Quality.DVDR.Id },
            { 24, Quality.WORKPRINT.Id },
            { 25, Quality.CAM.Id },
            { 26, Quality.TELESYNC.Id },
            { 27, Quality.TELECINE.Id },
            { 28, Quality.DVDSCR.Id },
            { 29, Quality.REGIONAL.Id },
            { 30, Quality.Remux1080p.Id },
            { 31, Quality.Remux2160p.Id },
        };

        private static readonly Dictionary<int, int> ToUpstreamMap = ToCatalogMap.ToDictionary(p => p.Value, p => p.Key);

        public static IReadOnlyDictionary<int, int> UpstreamToCatalog => ToCatalogMap;

        // Throws for an ID upstream never had: stored data with one is corrupt.
        public static int ToCatalog(int upstreamId)
        {
            if (!ToCatalogMap.TryGetValue(upstreamId, out var catalogId))
            {
                throw new KeyNotFoundException($"Radarr has no quality with ID {upstreamId}");
            }

            return catalogId;
        }

        // False when Radarr has no equivalent (for example a catalog entry only
        // another app parses). Callers leave the item out.
        public static bool TryToUpstream(int catalogId, out int upstreamId)
        {
            return ToUpstreamMap.TryGetValue(catalogId, out upstreamId);
        }
    }
}
