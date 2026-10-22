using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Movies.AutoTagging.Specifications;

namespace NzbDrone.Core.Movies.AutoTagging
{
    public class AutoTag : ModelBase
    {
        public AutoTag()
        {
            Tags = new HashSet<int>();
        }

        public string Name { get; set; }
        public List<IAutoTaggingSpecification> Specifications { get; set; }
        public bool RemoveTagsAutomatically { get; set; }
        public HashSet<int> Tags { get; set; }
    }
}
