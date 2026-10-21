using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download
{
    public interface IReleaseBlocklist
    {
        bool IsTorrentHashBlocklisted(RemoteRelease release, string hash);
    }

    // Routes a blocklist question to the module that owns the release.
    public class ReleaseBlocklist : IReleaseBlocklist
    {
        private readonly IEnumerable<IMediaBlocklist> _blocklists;

        public ReleaseBlocklist(IEnumerable<IMediaBlocklist> blocklists)
        {
            _blocklists = blocklists;
        }

        public bool IsTorrentHashBlocklisted(RemoteRelease release, string hash)
        {
            var blocklist = _blocklists.FirstOrDefault(b => b.Kind == release.Kind);

            return blocklist != null && blocklist.IsTorrentHashBlocklisted(release, hash);
        }
    }
}
