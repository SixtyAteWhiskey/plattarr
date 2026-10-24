using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Music.MediaFiles
{
    // What the file scan read from an audio file (Lidarr's MediaInfoModel). Music keeps its own
    // type: the video one carries streams, resolution and HDR format.
    public class MediaInfoModel : IEmbeddedDocument
    {
        public string AudioFormat { get; set; }
        public int AudioBitrate { get; set; }
        public int AudioChannels { get; set; }
        public int AudioBits { get; set; }
        public int AudioSampleRate { get; set; }
    }
}
