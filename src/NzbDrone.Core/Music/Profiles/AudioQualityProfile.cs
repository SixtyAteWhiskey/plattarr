using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Profiles
{
    // Music's quality profile (Lidarr's QualityProfile), kept in Music_QualityProfiles. It holds
    // audio qualities only; the video profile table is untouched. Cutoff is an audio quality ID,
    // or a group ID (1000 and up).
    public class AudioQualityProfile : ModelBase
    {
        public AudioQualityProfile()
        {
            FormatItems = new List<ProfileFormatItem>();
        }

        public string Name { get; set; }
        public bool UpgradeAllowed { get; set; }
        public int Cutoff { get; set; }
        public int MinFormatScore { get; set; }
        public int CutoffFormatScore { get; set; }
        public List<ProfileFormatItem> FormatItems { get; set; }
        public List<AudioQualityProfileItem> Items { get; set; }

        public AudioQuality FirstAllowedQuality()
        {
            var firstAllowed = Items.First(q => q.Allowed);

            if (firstAllowed.Quality != null)
            {
                return firstAllowed.Quality;
            }

            // Returning any item from the group will work,
            // returning the first because it's the true first quality.
            return firstAllowed.Items.First().Quality;
        }

        public AudioQuality LastAllowedQuality()
        {
            var lastAllowed = Items.Last(q => q.Allowed);

            if (lastAllowed.Quality != null)
            {
                return lastAllowed.Quality;
            }

            // Returning any item from the group will work,
            // returning the last because it's the true last quality.
            return lastAllowed.Items.Last().Quality;
        }

        public QualityIndex GetIndex(AudioQuality quality, bool respectGroupOrder = false)
        {
            return GetIndex(quality.Id, respectGroupOrder);
        }

        public QualityIndex GetIndex(int id, bool respectGroupOrder = false)
        {
            for (var i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                var quality = item.Quality;

                // Quality matches by ID
                if (quality != null && quality.Id == id)
                {
                    return new QualityIndex(i);
                }

                // Group matches by ID
                if (item.Id > 0 && item.Id == id)
                {
                    return new QualityIndex(i);
                }

                for (var g = 0; g < item.Items.Count; g++)
                {
                    var groupItem = item.Items[g];

                    if (groupItem.Quality.Id == id)
                    {
                        return respectGroupOrder ? new QualityIndex(i, g) : new QualityIndex(i);
                    }
                }
            }

            return new QualityIndex();
        }

        public int CalculateCustomFormatScore(List<CustomFormat> formats)
        {
            return FormatItems.Where(x => formats.Contains(x.Format)).Sum(x => x.Score);
        }
    }
}
