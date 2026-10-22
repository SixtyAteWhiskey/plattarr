using System;
using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Tv
{
    // Step 1 of the TV port registers the module so its tables, categories and download client
    // settings exist. Nothing searches, parses or imports TV yet: those arrive with their steps.
    public class TvModule : IMediaModule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.TV.Id };

        public MediaKind Kind => MediaKind.Tv;

        public string Name => "Tv";

        public IReadOnlyList<int> DefaultCategories => Categories;

        // Sonarr's scheduled commands (series refresh, scene mapping update, daily series update)
        // are added with the services that run them. A task whose command does not exist would
        // fail on every run.
        public IEnumerable<ScheduledTask> ScheduledTasks => Array.Empty<ScheduledTask>();
    }
}
