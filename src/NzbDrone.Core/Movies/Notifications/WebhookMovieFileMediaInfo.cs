using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.MediaFiles.MediaInfo;

namespace NzbDrone.Core.Movies.Notifications
{
    public class WebhookMovieFileMediaInfo
    {
        public WebhookMovieFileMediaInfo()
        {
        }

        public WebhookMovieFileMediaInfo(MovieFile movieFile)
        {
            AudioChannels = MediaInfoFormatter.FormatAudioChannels(movieFile.MediaInfo);
            AudioCodec = MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, movieFile.SceneName);
            AudioLanguages = movieFile.MediaInfo.AudioLanguages.Distinct().ToList();
            Height = movieFile.MediaInfo.Height;
            Width = movieFile.MediaInfo.Width;
            Subtitles = movieFile.MediaInfo.Subtitles.Distinct().ToList();
            VideoCodec = MediaInfoFormatter.FormatVideoCodec(movieFile.MediaInfo, movieFile.SceneName);
            VideoDynamicRange = MediaInfoFormatter.FormatVideoDynamicRange(movieFile.MediaInfo);
            VideoDynamicRangeType = MediaInfoFormatter.FormatVideoDynamicRangeType(movieFile.MediaInfo);
        }

        public decimal AudioChannels { get; set; }
        public string AudioCodec { get; set; }
        public List<string> AudioLanguages { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public List<string> Subtitles { get; set; }
        public string VideoCodec { get; set; }
        public string VideoDynamicRange { get; set; }
        public string VideoDynamicRangeType { get; set; }
    }
}
