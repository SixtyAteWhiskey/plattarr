using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Profiles
{
    // One entry of an audio quality profile: a quality, or a named group of qualities
    // (Lidarr's QualityProfileQualityItem). Group IDs are 1000 and up.
    public class AudioQualityProfileItem : IEmbeddedDocument
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int Id { get; set; }

        public string Name { get; set; }
        public AudioQuality Quality { get; set; }
        public List<AudioQualityProfileItem> Items { get; set; }
        public bool Allowed { get; set; }

        public AudioQualityProfileItem()
        {
            Items = new List<AudioQualityProfileItem>();
        }

        public List<AudioQuality> GetQualities()
        {
            if (Quality == null)
            {
                return Items.Select(s => s.Quality).ToList();
            }

            return new List<AudioQuality> { Quality };
        }

        public override string ToString()
        {
            var qualitiesString = string.Join(", ", GetQualities());

            if (Name.IsNotNullOrWhiteSpace())
            {
                return $"{Name} ({qualitiesString})";
            }

            return qualitiesString;
        }
    }
}
