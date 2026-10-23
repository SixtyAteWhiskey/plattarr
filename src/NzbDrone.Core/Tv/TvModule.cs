using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Core.Tv.DataAugmentation.Scene;

namespace NzbDrone.Core.Tv
{
    // Step 1 of the TV port registers the module so its tables, categories and download client
    // settings exist. Step 3 adds the metadata services and their two scheduled commands. Nothing
    // searches or imports TV yet: those arrive with their steps.
    public class TvModule : IMediaModule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.TV.Id };

        public MediaKind Kind => MediaKind.Tv;

        public string Name => "Tv";

        public IReadOnlyList<int> DefaultCategories => Categories;

        // Sonarr's scheduled commands are added with the services that run them. A task whose
        // command does not exist would fail on every run. Intervals are Sonarr's: the scene
        // mapping update every three hours and the series refresh every twelve.
        public IEnumerable<ScheduledTask> ScheduledTasks => new[]
        {
            new ScheduledTask
            {
                Interval = 3 * 60,
                TypeName = typeof(UpdateSceneMappingCommand).FullName
            },

            new ScheduledTask
            {
                Interval = 12 * 60,
                TypeName = typeof(RefreshSeriesCommand).FullName
            }
        };
    }
}
