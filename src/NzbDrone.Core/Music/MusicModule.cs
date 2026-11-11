using System.Collections.Generic;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Commands;
using NzbDrone.Core.Music.MediaFiles.Commands;
using NzbDrone.Core.Music.Search;

namespace NzbDrone.Core.Music
{
    // The Music module registers its own tables, categories and scheduled work.
    public class MusicModule : IMediaModule, IConfiguredSchedule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.Audio.Id };

        public MediaKind Kind => MediaKind.Music;

        public string Name => "Music";

        public IReadOnlyList<int> DefaultCategories => Categories;

        public IReadOnlyDictionary<string, int> ConfiguredIntervals(IConfigService config)
        {
            return new Dictionary<string, int>
            {
                [typeof(ProactiveMusicSearchCommand).FullName] = config.ProactiveMusicSearchIntervalMinutes
            };
        }

        // Artist refresh and root-folder rescan retain their 24-hour cadence. Proactive album
        // search has a separate configurable interval and stays disabled until explicitly enabled.
        public IEnumerable<ScheduledTask> ScheduledTasks => new[]
        {
            new ScheduledTask
            {
                Interval = 60,
                TypeName = typeof(ProactiveMusicSearchCommand).FullName
            },
            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(RefreshArtistCommand).FullName
            },

            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(RescanFoldersCommand).FullName
            }
        };
    }
}
