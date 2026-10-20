using System.Collections.Generic;
using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Movies.Search
{
    public class MoviesSearchCommand : Command
    {
        public List<int> MovieIds { get; set; }

        public override bool SendUpdatesToClient => true;
    }
}
