using System;
using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music
{
    // Step 1 of the Music port registers the module so its tables, categories and download client
    // settings exist. Lidarr's scheduled commands (artist refresh, metadata source) are added with
    // the services that run them in step 3: a task whose command does not exist fails on every run.
    // Nothing searches or imports Music yet.
    public class MusicModule : IMediaModule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.Audio.Id };

        public MediaKind Kind => MediaKind.Music;

        public string Name => "Music";

        public IReadOnlyList<int> DefaultCategories => Categories;

        public IEnumerable<ScheduledTask> ScheduledTasks => Array.Empty<ScheduledTask>();
    }
}
