using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Movies.Search
{
    public class CutoffUnmetMoviesSearchCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public string FilterKey { get; set; }
        public string FilterValue { get; set; }
    }
}
