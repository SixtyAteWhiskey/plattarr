using System.Collections.Generic;

namespace NzbDrone.Core.Modules
{
    // The core tag service decides whether a tag is in use and reports where. The module owns the
    // library entries that carry tags, so it says which tags each one has. One source per module,
    // picked by Kind.
    public interface ITaggedItemSource
    {
        MediaKind Kind { get; }

        // Library entry id to the ids of the tags it carries.
        Dictionary<int, List<int>> AllItemTags();
    }
}
