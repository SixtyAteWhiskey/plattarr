using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Movies.Blocklisting
{
    public class MovieBlocklist : IMediaBlocklist
    {
        private readonly IBlocklistService _blocklistService;

        public MovieBlocklist(IBlocklistService blocklistService)
        {
            _blocklistService = blocklistService;
        }

        public MediaKind Kind => MediaKind.Movies;

        public bool IsTorrentHashBlocklisted(RemoteRelease release, string hash)
        {
            return release.Parent != null && _blocklistService.BlocklistedTorrentHash(release.Parent.Id, hash);
        }
    }
}
