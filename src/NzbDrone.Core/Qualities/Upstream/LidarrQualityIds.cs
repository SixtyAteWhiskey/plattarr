using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Qualities.Upstream
{
    // Two-way map between Lidarr's quality IDs and Plattarr's audio catalog IDs
    // (docs/catalogs.md). Used by the migration importer and the compatibility APIs.
    // Upstream IDs come from Lidarr aec88028.
    public static class LidarrQualityIds
    {
        private static readonly Dictionary<int, int> ToCatalogMap = new()
        {
            { 0, AudioQuality.Unknown.Id },
            { 1, AudioQuality.MP3_192.Id },
            { 2, AudioQuality.MP3_VBR.Id },
            { 3, AudioQuality.MP3_256.Id },
            { 4, AudioQuality.MP3_320.Id },
            { 5, AudioQuality.MP3_160.Id },
            { 6, AudioQuality.FLAC.Id },
            { 7, AudioQuality.ALAC.Id },
            { 8, AudioQuality.MP3_VBR_V2.Id },
            { 9, AudioQuality.AAC_192.Id },
            { 10, AudioQuality.AAC_256.Id },
            { 11, AudioQuality.AAC_320.Id },
            { 12, AudioQuality.AAC_VBR.Id },
            { 13, AudioQuality.WAV.Id },
            { 14, AudioQuality.VORBIS_Q10.Id },
            { 15, AudioQuality.VORBIS_Q9.Id },
            { 16, AudioQuality.VORBIS_Q8.Id },
            { 17, AudioQuality.VORBIS_Q7.Id },
            { 18, AudioQuality.VORBIS_Q6.Id },
            { 19, AudioQuality.VORBIS_Q5.Id },
            { 20, AudioQuality.WMA.Id },
            { 21, AudioQuality.FLAC_24.Id },
            { 22, AudioQuality.MP3_128.Id },
            { 23, AudioQuality.MP3_096.Id },
            { 24, AudioQuality.MP3_080.Id },
            { 25, AudioQuality.MP3_064.Id },
            { 26, AudioQuality.MP3_056.Id },
            { 27, AudioQuality.MP3_048.Id },
            { 28, AudioQuality.MP3_040.Id },
            { 29, AudioQuality.MP3_032.Id },
            { 30, AudioQuality.MP3_024.Id },
            { 31, AudioQuality.MP3_016.Id },
            { 32, AudioQuality.MP3_008.Id },
            { 33, AudioQuality.MP3_112.Id },
            { 34, AudioQuality.MP3_224.Id },
            { 35, AudioQuality.APE.Id },
            { 36, AudioQuality.WAVPACK.Id },
            { 37, AudioQuality.ALAC_24.Id },
        };

        private static readonly Dictionary<int, int> ToUpstreamMap = ToCatalogMap.ToDictionary(p => p.Value, p => p.Key);

        public static IReadOnlyDictionary<int, int> UpstreamToCatalog => ToCatalogMap;

        // Throws for an ID Lidarr never had: stored data with one is corrupt.
        public static int ToCatalog(int upstreamId)
        {
            if (!ToCatalogMap.TryGetValue(upstreamId, out var catalogId))
            {
                throw new KeyNotFoundException($"Lidarr has no quality with ID {upstreamId}");
            }

            return catalogId;
        }

        // False for an ID outside the audio catalog (a video quality). Callers leave the item out.
        public static bool TryToUpstream(int catalogId, out int upstreamId)
        {
            return ToUpstreamMap.TryGetValue(catalogId, out upstreamId);
        }
    }
}
