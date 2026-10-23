using System.Collections.Generic;

namespace NzbDrone.Core.Modules
{
    // A module owns its auto tags (Tv_AutoTagging, AutoTagging for movies). The core tag service and
    // tag cleanup must know which tags they use, either as the tags they apply or inside a tag
    // condition, so the module says. One source per module, picked by Kind.
    public interface IAutoTagUsageSource
    {
        MediaKind Kind { get; }

        List<AutoTagUsage> All();
    }

    public class AutoTagUsage
    {
        public int Id { get; set; }

        // The tags the auto tag applies.
        public ICollection<int> Tags { get; set; } = new List<int>();

        // The tags its conditions look for.
        public ICollection<int> ConditionTags { get; set; } = new List<int>();

        public bool Uses(int tagId)
        {
            return Tags.Contains(tagId) || ConditionTags.Contains(tagId);
        }
    }
}
