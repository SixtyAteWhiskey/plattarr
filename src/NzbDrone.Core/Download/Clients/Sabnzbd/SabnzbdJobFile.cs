using Newtonsoft.Json;

namespace NzbDrone.Core.Download.Clients.Sabnzbd
{
    public class SabnzbdJobFile
    {
        [JsonProperty("filename")]
        public string Name { get; set; }

        [JsonProperty("nzf_id")]
        public string Id { get; set; }
    }
}
