using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Movies.AutoTagging.Specifications;

namespace NzbDrone.Core.Movies.AutoTagging
{
    public class SpecificationMatchesGroup
    {
        public Dictionary<IAutoTaggingSpecification, bool> Matches { get; set; }

        public bool DidMatch => !(Matches.Any(m => m.Key.Required && m.Value == false) ||
                                  Matches.All(m => m.Value == false));
    }
}
