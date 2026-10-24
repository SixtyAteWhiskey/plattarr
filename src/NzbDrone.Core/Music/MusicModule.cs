using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Commands;

namespace NzbDrone.Core.Music
{
    // Step 1 of the Music port registers the module so its tables, categories and download client
    // settings exist. Step 3 adds the metadata services and their scheduled command. Nothing
    // searches or imports Music yet: those arrive with their steps.
    public class MusicModule : IMediaModule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.Audio.Id };

        public MediaKind Kind => MediaKind.Music;

        public string Name => "Music";

        public IReadOnlyList<int> DefaultCategories => Categories;

        // Lidarr's scheduled commands are added with the services that run them. A task whose
        // command does not exist would fail on every run. The interval is Lidarr's: the artist
        // refresh runs every 24 hours.
        public IEnumerable<ScheduledTask> ScheduledTasks => new[]
        {
            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(RefreshArtistCommand).FullName
            }
        };
    }
}
