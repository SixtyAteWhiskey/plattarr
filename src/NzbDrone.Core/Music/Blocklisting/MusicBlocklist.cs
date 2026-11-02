using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music.Blocklisting
{
    public class MusicBlocklist : IMediaBlocklist
    {
        private readonly IBlocklistService _blocklistService;

        public MusicBlocklist(IBlocklistService blocklistService)
        {
            _blocklistService = blocklistService;
        }

        public MediaKind Kind => MediaKind.Music;

        public bool IsTorrentHashBlocklisted(RemoteRelease release, string hash)
        {
            return release.Parent != null && _blocklistService.BlocklistedTorrentHash(release.Parent.Id, hash);
        }
    }
}
