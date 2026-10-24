using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Qualities
{
    // Plattarr's audio quality catalog (docs/catalogs.md). Music uses it instead of Quality, so
    // an audio quality never appears in a video profile, parser or API resource. IDs are
    // Plattarr's own: 0 is Unknown and the rest take 201 to 399 in Lidarr's default weight
    // order at birth (ties in the order of Lidarr's default definitions). An ID is never reused
    // or renumbered. Lidarr's own IDs (0 to 37) translate through Qualities.Upstream.
    // Names, weights, groups and sizes are Lidarr aec88028.
    public class AudioQuality : IEmbeddedDocument, IEquatable<AudioQuality>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public AudioQuality()
        {
        }

        private AudioQuality(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(AudioQuality other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as AudioQuality);
        }

        public static bool operator ==(AudioQuality left, AudioQuality right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(AudioQuality left, AudioQuality right)
        {
            return !Equals(left, right);
        }

        public static AudioQuality Unknown => new AudioQuality(0, "Unknown");
        public static AudioQuality MP3_008 => new AudioQuality(201, "MP3-8");
        public static AudioQuality MP3_016 => new AudioQuality(202, "MP3-16");
        public static AudioQuality MP3_024 => new AudioQuality(203, "MP3-24");
        public static AudioQuality MP3_032 => new AudioQuality(204, "MP3-32");
        public static AudioQuality MP3_040 => new AudioQuality(205, "MP3-40");
        public static AudioQuality MP3_048 => new AudioQuality(206, "MP3-48");
        public static AudioQuality MP3_056 => new AudioQuality(207, "MP3-56");
        public static AudioQuality MP3_064 => new AudioQuality(208, "MP3-64");
        public static AudioQuality MP3_080 => new AudioQuality(209, "MP3-80");
        public static AudioQuality MP3_096 => new AudioQuality(210, "MP3-96");
        public static AudioQuality MP3_112 => new AudioQuality(211, "MP3-112");
        public static AudioQuality MP3_128 => new AudioQuality(212, "MP3-128");
        public static AudioQuality VORBIS_Q5 => new AudioQuality(213, "OGG Vorbis Q5");
        public static AudioQuality MP3_160 => new AudioQuality(214, "MP3-160");
        public static AudioQuality MP3_192 => new AudioQuality(215, "MP3-192");
        public static AudioQuality VORBIS_Q6 => new AudioQuality(216, "OGG Vorbis Q6");
        public static AudioQuality AAC_192 => new AudioQuality(217, "AAC-192");
        public static AudioQuality WMA => new AudioQuality(218, "WMA");
        public static AudioQuality MP3_224 => new AudioQuality(219, "MP3-224");
        public static AudioQuality VORBIS_Q7 => new AudioQuality(220, "OGG Vorbis Q7");
        public static AudioQuality MP3_VBR_V2 => new AudioQuality(221, "MP3-VBR-V2");
        public static AudioQuality MP3_256 => new AudioQuality(222, "MP3-256");
        public static AudioQuality VORBIS_Q8 => new AudioQuality(223, "OGG Vorbis Q8");
        public static AudioQuality AAC_256 => new AudioQuality(224, "AAC-256");
        public static AudioQuality MP3_VBR => new AudioQuality(225, "MP3-VBR-V0");
        public static AudioQuality AAC_VBR => new AudioQuality(226, "AAC-VBR");
        public static AudioQuality MP3_320 => new AudioQuality(227, "MP3-320");
        public static AudioQuality VORBIS_Q9 => new AudioQuality(228, "OGG Vorbis Q9");
        public static AudioQuality AAC_320 => new AudioQuality(229, "AAC-320");
        public static AudioQuality VORBIS_Q10 => new AudioQuality(230, "OGG Vorbis Q10");
        public static AudioQuality FLAC => new AudioQuality(231, "FLAC");
        public static AudioQuality ALAC => new AudioQuality(232, "ALAC");
        public static AudioQuality APE => new AudioQuality(233, "APE");
        public static AudioQuality WAVPACK => new AudioQuality(234, "WavPack");
        public static AudioQuality FLAC_24 => new AudioQuality(235, "FLAC 24bit");
        public static AudioQuality ALAC_24 => new AudioQuality(236, "ALAC 24bit");
        public static AudioQuality WAV => new AudioQuality(237, "WAV");

        static AudioQuality()
        {
            All = new List<AudioQuality>
            {
                Unknown,
                MP3_008,
                MP3_016,
                MP3_024,
                MP3_032,
                MP3_040,
                MP3_048,
                MP3_056,
                MP3_064,
                MP3_080,
                MP3_096,
                MP3_112,
                MP3_128,
                VORBIS_Q5,
                MP3_160,
                MP3_192,
                VORBIS_Q6,
                AAC_192,
                WMA,
                MP3_224,
                VORBIS_Q7,
                MP3_VBR_V2,
                MP3_256,
                VORBIS_Q8,
                AAC_256,
                MP3_VBR,
                AAC_VBR,
                MP3_320,
                VORBIS_Q9,
                AAC_320,
                VORBIS_Q10,
                FLAC,
                ALAC,
                APE,
                WAVPACK,
                FLAC_24,
                ALAC_24,
                WAV
            };

            AllLookup = All.ToDictionary(q => q.Id);

            DefaultQualityDefinitions = new HashSet<AudioQualityDefinition>
            {
                new AudioQualityDefinition(AudioQuality.Unknown) { Weight = 1, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupWeight = 1 },
                new AudioQualityDefinition(AudioQuality.MP3_008) { Weight = 2, MinSize = 0, MaxSize = 10, PreferredSize = 5, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_016) { Weight = 3, MinSize = 0, MaxSize = 20, PreferredSize = 15, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_024) { Weight = 4, MinSize = 0, MaxSize = 30, PreferredSize = 25, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_032) { Weight = 5, MinSize = 0, MaxSize = 40, PreferredSize = 35, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_040) { Weight = 6, MinSize = 0, MaxSize = 45, PreferredSize = 40, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_048) { Weight = 7, MinSize = 0, MaxSize = 55, PreferredSize = 50, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_056) { Weight = 8, MinSize = 0, MaxSize = 65, PreferredSize = 60, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_064) { Weight = 9, MinSize = 0, MaxSize = 75, PreferredSize = 70, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_080) { Weight = 10, MinSize = 0, MaxSize = 95, PreferredSize = 90, GroupName = "Trash Quality Lossy", GroupWeight = 2 },
                new AudioQualityDefinition(AudioQuality.MP3_096) { Weight = 11, MinSize = 0, MaxSize = 110, PreferredSize = 95, GroupName = "Poor Quality Lossy", GroupWeight = 3 },
                new AudioQualityDefinition(AudioQuality.MP3_112) { Weight = 12, MinSize = 0, MaxSize = 125, PreferredSize = 95, GroupName = "Poor Quality Lossy", GroupWeight = 3 },
                new AudioQualityDefinition(AudioQuality.MP3_128) { Weight = 13, MinSize = 0, MaxSize = 140, PreferredSize = 95, GroupName = "Poor Quality Lossy", GroupWeight = 3 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q5) { Weight = 14, MinSize = 0, MaxSize = 175, PreferredSize = 95, GroupName = "Poor Quality Lossy", GroupWeight = 3 },
                new AudioQualityDefinition(AudioQuality.MP3_160) { Weight = 14, MinSize = 0, MaxSize = 175, PreferredSize = 95, GroupName = "Poor Quality Lossy", GroupWeight = 3 },
                new AudioQualityDefinition(AudioQuality.MP3_192) { Weight = 15, MinSize = 0, MaxSize = 210, PreferredSize = 95, GroupName = "Low Quality Lossy", GroupWeight = 4 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q6) { Weight = 15, MinSize = 0, MaxSize = 210, PreferredSize = 95, GroupName = "Low Quality Lossy", GroupWeight = 4 },
                new AudioQualityDefinition(AudioQuality.AAC_192) { Weight = 15, MinSize = 0, MaxSize = 210, PreferredSize = 95, GroupName = "Low Quality Lossy", GroupWeight = 4 },
                new AudioQualityDefinition(AudioQuality.WMA) { Weight = 15, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "Low Quality Lossy", GroupWeight = 4 },
                new AudioQualityDefinition(AudioQuality.MP3_224) { Weight = 16, MinSize = 0, MaxSize = 245, PreferredSize = 95, GroupName = "Low Quality Lossy", GroupWeight = 4 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q7) { Weight = 17, MinSize = 0, MaxSize = 245, PreferredSize = 95, GroupName = "Mid Quality Lossy", GroupWeight = 5 },
                new AudioQualityDefinition(AudioQuality.MP3_VBR_V2) { Weight = 18, MinSize = 0, MaxSize = 280, PreferredSize = 95, GroupName = "Mid Quality Lossy", GroupWeight = 5 },
                new AudioQualityDefinition(AudioQuality.MP3_256) { Weight = 18, MinSize = 0, MaxSize = 280, PreferredSize = 95, GroupName = "Mid Quality Lossy", GroupWeight = 5 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q8) { Weight = 18, MinSize = 0, MaxSize = 280, PreferredSize = 95, GroupName = "Mid Quality Lossy", GroupWeight = 5 },
                new AudioQualityDefinition(AudioQuality.AAC_256) { Weight = 18, MinSize = 0, MaxSize = 280, PreferredSize = 95, GroupName = "Mid Quality Lossy", GroupWeight = 5 },
                new AudioQualityDefinition(AudioQuality.MP3_VBR) { Weight = 19, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.AAC_VBR) { Weight = 19, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.MP3_320) { Weight = 20, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q9) { Weight = 20, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.AAC_320) { Weight = 20, MinSize = 0, MaxSize = 350, PreferredSize = 195, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.VORBIS_Q10) { Weight = 21, MinSize = 0, MaxSize = 550, PreferredSize = 295, GroupName = "High Quality Lossy", GroupWeight = 6 },
                new AudioQualityDefinition(AudioQuality.FLAC) { Weight = 22, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.ALAC) { Weight = 22, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.APE) { Weight = 22, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.WAVPACK) { Weight = 22, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.FLAC_24) { Weight = 23, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.ALAC_24) { Weight = 23, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupName = "Lossless", GroupWeight = 7 },
                new AudioQualityDefinition(AudioQuality.WAV) { Weight = 24, MinSize = 0, MaxSize = null, PreferredSize = 895, GroupWeight = 8 }
            };
        }

        public static readonly List<AudioQuality> All;

        public static readonly IReadOnlyDictionary<int, AudioQuality> AllLookup;

        public static readonly HashSet<AudioQualityDefinition> DefaultQualityDefinitions;

        public static AudioQuality FindById(int id)
        {
            if (!AllLookup.TryGetValue(id, out var quality))
            {
                throw new ArgumentException("ID does not match a known audio quality", nameof(id));
            }

            return quality;
        }

        public static explicit operator AudioQuality(int id)
        {
            return FindById(id);
        }

        public static explicit operator int(AudioQuality quality)
        {
            return quality.Id;
        }
    }
}
