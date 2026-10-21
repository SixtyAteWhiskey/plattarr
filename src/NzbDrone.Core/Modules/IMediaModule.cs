using System.Collections.Generic;
using NzbDrone.Core.Jobs;

namespace NzbDrone.Core.Modules
{
    // Contract 1 in docs/module-interface.md. One implementation per media type, found by the
    // container like any other service. Table mapping is separate (IModuleTableMap) because it
    // runs before the container exists.
    public interface IMediaModule
    {
        MediaKind Kind { get; }

        // "Movies". Also the module's table prefix without the underscore.
        string Name { get; }

        // Top-level Newznab categories this media type searches and receives from RSS (2000 for
        // movies) when the indexer has no category override for it.
        IReadOnlyList<int> DefaultCategories { get; }

        // Scheduled tasks the module runs, added to the core's at startup. TypeName and Interval
        // are read; the stored row keeps its last run time.
        IEnumerable<ScheduledTask> ScheduledTasks { get; }
    }
}
