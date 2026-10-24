using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Qualities
{
    // The user-editable part of an audio quality: title, size limits and the group it falls in by
    // default. Lidarr's QualityDefinition. The rows live in Music_QualityDefinitions.
    public class AudioQualityDefinition : ModelBase
    {
        public AudioQuality Quality { get; set; }

        public string Title { get; set; }
        public string GroupName { get; set; }
        public int GroupWeight { get; set; }
        public int Weight { get; set; }

        public double? MinSize { get; set; }
        public double? MaxSize { get; set; }
        public double? PreferredSize { get; set; }

        public AudioQualityDefinition()
        {
        }

        public AudioQualityDefinition(AudioQuality quality)
        {
            Quality = quality;
            Title = quality.Name;
        }

        public override string ToString()
        {
            return Quality.Name;
        }
    }
}
