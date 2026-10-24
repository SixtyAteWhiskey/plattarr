using NzbDrone.Core.Music.MediaFiles;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.TrackFiles
{
    public class AudioMediaInfoResource : RestResource
    {
        public decimal AudioChannels { get; set; }
        public string AudioBitRate { get; set; }
        public string AudioCodec { get; set; }
        public string AudioBits { get; set; }
        public string AudioSampleRate { get; set; }
    }

    public static class AudioMediaInfoResourceMapper
    {
        public static AudioMediaInfoResource ToResource(this MediaInfoModel model)
        {
            if (model == null)
            {
                return null;
            }

            return new AudioMediaInfoResource
            {
                AudioChannels = MediaInfoFormatter.FormatAudioChannels(model),
                AudioCodec = MediaInfoFormatter.FormatAudioCodec(model),
                AudioBitRate = MediaInfoFormatter.FormatAudioBitrate(model),
                AudioBits = MediaInfoFormatter.FormatAudioBitsPerSample(model),
                AudioSampleRate = MediaInfoFormatter.FormatAudioSampleRate(model)
            };
        }
    }
}
