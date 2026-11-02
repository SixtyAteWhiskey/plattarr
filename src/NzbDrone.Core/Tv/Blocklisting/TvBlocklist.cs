using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv.Blocklisting
{
    public class TvBlocklist : IMediaBlocklist
    {
        private readonly IBlocklistService _blocklistService;

        public TvBlocklist(IBlocklistService blocklistService)
        {
            _blocklistService = blocklistService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public bool IsTorrentHashBlocklisted(RemoteRelease release, string hash)
        {
            return release.Parent != null && _blocklistService.BlocklistedTorrentHash(release.Parent.Id, hash);
        }
    }
}
