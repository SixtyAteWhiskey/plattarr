using System;
using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Parser.Model
{
    // Translates between the shared release model's flags (Prowlarr's named
    // IndexerFlag set plus torrent volume factors) and Radarr's IndexerFlags
    // enum. The enum stays the movie vocabulary because custom formats,
    // required-flag settings, movie files, blocklist rows, history data, and
    // the Radarr v3 API store its integer values.
    public static class IndexerFlagsMapper
    {
        // Named flags for enum values with no Prowlarr equivalent. "golden"
        // and "approved" match Prowlarr's PassThePopcornFlag names.
        public static IndexerFlag Golden => new("golden", "Release follows Golden Popcorn quality rules");
        public static IndexerFlag Approved => new("approved", "Release approved by PTP staff");
        public static IndexerFlag Nuked => new("nuked", "Release is nuked");
        public static IndexerFlag Freeleech75 => new("freeleech75", "Release counts 25% to ratio");
        public static IndexerFlag Freeleech25 => new("freeleech25", "Release counts 75% to ratio");

#pragma warning disable CS0612, CS0618
        public static IndexerFlag AhdInternal => new("ahd_internal", "AHD internal (obsolete)");
        public static IndexerFlag AhdUserRelease => new("ahd_userrelease", "AHD user release (obsolete)");

        private static readonly (IndexerFlags Value, IndexerFlag Flag)[] NamedFlags =
        {
            (IndexerFlags.G_Freeleech, IndexerFlag.FreeLeech),
            (IndexerFlags.G_Halfleech, IndexerFlag.HalfLeech),
            (IndexerFlags.G_DoubleUpload, IndexerFlag.DoubleUpload),
            (IndexerFlags.PTP_Golden, Golden),
            (IndexerFlags.PTP_Approved, Approved),
            (IndexerFlags.G_Internal, IndexerFlag.Internal),
            (IndexerFlags.AHD_Internal, AhdInternal),
            (IndexerFlags.G_Scene, IndexerFlag.Scene),
            (IndexerFlags.G_Freeleech75, Freeleech75),
            (IndexerFlags.G_Freeleech25, Freeleech25),
            (IndexerFlags.AHD_UserRelease, AhdUserRelease),
            (IndexerFlags.Nuked, Nuked)
        };
#pragma warning restore CS0612, CS0618

        public static IndexerFlags ToIndexerFlags(this ReleaseInfo release)
        {
            IndexerFlags flags = 0;

            if (release == null)
            {
                return flags;
            }

            if (release.IndexerFlags != null)
            {
                foreach (var (value, flag) in NamedFlags)
                {
                    if (release.IndexerFlags.Contains(flag))
                    {
                        flags |= value;
                    }
                }
            }

            if (release is TorrentInfo torrent)
            {
                // Same factor mapping as Radarr's Torznab parser.
                switch (torrent.DownloadVolumeFactor)
                {
                    case 0.0:
                        flags |= IndexerFlags.G_Freeleech;
                        break;
                    case 0.25:
                        flags |= IndexerFlags.G_Freeleech75;
                        break;
                    case 0.5:
                        flags |= IndexerFlags.G_Halfleech;
                        break;
                    case 0.75:
                        flags |= IndexerFlags.G_Freeleech25;
                        break;
                }

                if (torrent.UploadVolumeFactor == 2.0)
                {
                    flags |= IndexerFlags.G_DoubleUpload;
                }

                if (torrent.Scene == true)
                {
                    flags |= IndexerFlags.G_Scene;
                }
            }

            return flags;
        }

        // Named flags for every bit set in the enum.
        public static HashSet<IndexerFlag> ToIndexerFlagSet(this IndexerFlags flags)
        {
            var result = new HashSet<IndexerFlag>();

            foreach (var (value, flag) in NamedFlags)
            {
                if ((flags & value) == value)
                {
                    result.Add(flag);
                }
            }

            return result;
        }

        // Adds the named flag for every bit set in the enum. Existing flags and
        // volume factors are left alone, so ToIndexerFlags returns at least the
        // flags passed in.
        public static void AddIndexerFlags(this ReleaseInfo release, IndexerFlags flags)
        {
            ArgumentNullException.ThrowIfNull(release);

            release.IndexerFlags ??= new HashSet<IndexerFlag>();
            release.IndexerFlags.UnionWith(flags.ToIndexerFlagSet());
        }
    }
}
