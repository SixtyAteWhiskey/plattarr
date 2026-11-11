using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Movies.Search
{
    public class ProactiveMovieUpgradeCommand : Command
    {
        public override bool SendUpdatesToClient => true;
    }
}
