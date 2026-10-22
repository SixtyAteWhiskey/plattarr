using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

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

        // Sonarr's scheduled commands are added with the services that run them. A task whose
        // command does not exist would fail on every run. The scene mapping update (every three
        // hours, as Sonarr) is here; the series refresh and daily series update come with step 3.
        public IEnumerable<ScheduledTask> ScheduledTasks => new[]
        {
            new ScheduledTask
            {
                Interval = 3 * 60,
                TypeName = typeof(UpdateSceneMappingCommand).FullName
            }
        };
    }
}
