using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.Blocklisting
{
    // Clears the Music blocklist. Lidarr names this ClearBlocklist; the movie module already owns that
    // command name (a command is looked up by its type name), so the Music one is ClearMusicBlocklist.
    public class ClearMusicBlocklistCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
