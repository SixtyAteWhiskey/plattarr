using System.Collections.Generic;

namespace NzbDrone.Core.Modules
{
    // Contract 7 in docs/module-interface.md. A media module turns one of its parents into the
    // display values the core shows: notification providers today, activity and calendar rows later.
    // One implementation per media type, found by Kind.
    public interface IActivityRenderer
    {
        MediaKind Kind { get; }

        // Loads the parent and describes it. itemIds narrows the label to the items involved
        // (episodes, tracks); Movies has no separate items and ignores it.
        ActivitySubject Describe(int parentId, IReadOnlyList<int> itemIds);
    }
}
