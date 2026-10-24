using System.Collections.Generic;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.Qualities
{
    // Lidarr's QualityModelComparer over the audio catalog: orders qualities by their position in an
    // audio quality profile, then by revision.
    public class AudioQualityModelComparer : IComparer<AudioQuality>, IComparer<AudioQualityModel>
    {
        private readonly AudioQualityProfile _profile;

        public AudioQualityModelComparer(AudioQualityProfile profile)
        {
            Ensure.That(profile, () => profile).IsNotNull();
            Ensure.That(profile.Items, () => profile.Items).HasItems();

            _profile = profile;
        }

        public int Compare(int left, int right, bool respectGroupOrder = false)
        {
            var leftIndex = _profile.GetIndex(left);
            var rightIndex = _profile.GetIndex(right);

            return leftIndex.CompareTo(rightIndex, respectGroupOrder);
        }

        public int Compare(AudioQuality left, AudioQuality right)
        {
            return Compare(left, right, false);
        }

        public int Compare(AudioQuality left, AudioQuality right, bool respectGroupOrder)
        {
            var leftIndex = _profile.GetIndex(left, respectGroupOrder);
            var rightIndex = _profile.GetIndex(right, respectGroupOrder);

            return leftIndex.CompareTo(rightIndex, respectGroupOrder);
        }

        public int Compare(AudioQualityModel left, AudioQualityModel right)
        {
            return Compare(left, right, false);
        }

        public int Compare(AudioQualityModel left, AudioQualityModel right, bool respectGroupOrder)
        {
            var result = Compare(left.Quality, right.Quality, respectGroupOrder);

            if (result == 0)
            {
                result = left.Revision.CompareTo(right.Revision);
            }

            return result;
        }
    }
}
