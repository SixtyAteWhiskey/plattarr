using System;
using System.Collections.Generic;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Parser.Upstream
{
    // Sonarr's IndexerFlags enum uses other bit values than Radarr's from bit 8 up (Sonarr 8 is
    // Internal, Radarr 8 is PTP_Golden). Plattarr keeps Radarr's values as the global vocabulary
    // (IndexerFlagsMapper) and translates Sonarr's by name. Used by the migration importer and the
    // compatibility APIs. Values come from Sonarr develop b3d4bb8.
    public static class SonarrIndexerFlags
    {
        private const int AllUpstreamBits = 1 | 2 | 4 | 8 | 16 | 32 | 64 | 128;

        private static readonly (int Upstream, IndexerFlags Global)[] Map =
        {
            (1, IndexerFlags.G_Freeleech),
            (2, IndexerFlags.G_Halfleech),
            (4, IndexerFlags.G_DoubleUpload),
            (8, IndexerFlags.G_Internal),
            (16, IndexerFlags.G_Scene),
            (32, IndexerFlags.G_Freeleech75),
            (64, IndexerFlags.G_Freeleech25),
            (128, IndexerFlags.Nuked)
        };

        public static IReadOnlyList<(int Upstream, IndexerFlags Global)> Flags => Map;

        // Throws for a bit Sonarr never had: stored data with one is corrupt.
        public static IndexerFlags ToGlobal(int upstreamFlags)
        {
            if ((upstreamFlags & ~AllUpstreamBits) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upstreamFlags), $"Sonarr has no indexer flag with value {upstreamFlags & ~AllUpstreamBits}");
            }

            IndexerFlags result = 0;

            foreach (var (upstream, global) in Map)
            {
                if ((upstreamFlags & upstream) == upstream)
                {
                    result |= global;
                }
            }

            return result;
        }

        // The Sonarr integer for the flags Sonarr knows. Flags it has no equivalent for
        // (PTP_Golden, PTP_Approved and the obsolete AHD ones) are dropped.
        public static int ToUpstream(IndexerFlags globalFlags)
        {
            var result = 0;

            foreach (var (upstream, global) in Map)
            {
                if ((globalFlags & global) == global)
                {
                    result |= upstream;
                }
            }

            return result;
        }
    }
}
