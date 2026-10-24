using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace Radarr.Api.V3.Music.TrackFiles
{
    public class TrackFileListResource
    {
        public List<int> TrackFileIds { get; set; }
        public AudioQualityModel Quality { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
    }
}
