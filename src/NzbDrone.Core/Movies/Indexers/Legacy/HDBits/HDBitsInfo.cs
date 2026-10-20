using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Indexers.Legacy.HDBits
{
    public class HDBitsInfo : TorrentInfo
    {
        public bool? Internal { get; set; }
    }
}
