using Newtonsoft.Json;

namespace NzbDrone.Core.Download.Clients.QBittorrent
{
    public class QBittorrentTransferInfo
    {
        [JsonProperty(PropertyName = "connection_status")]
        public string ConnectionStatus { get; set; }
    }
}
