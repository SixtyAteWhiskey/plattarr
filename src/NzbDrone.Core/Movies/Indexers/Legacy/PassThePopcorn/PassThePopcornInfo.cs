using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Movies.Indexers.Legacy.PassThePopcorn
{
    public class PassThePopcornInfo : TorrentInfo
    {
        public bool? Golden { get; set; }
        public bool? Scene { get; set; }
        public bool? Approved { get; set; }
    }
}
