using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Tv.Blocklisting
{
    // Clears the series blocklist. Sonarr names this ClearBlocklist; the movie module already owns
    // that command name (a command is looked up by its type name), so the TV one is ClearTvBlocklist.
    public class ClearTvBlocklistCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
