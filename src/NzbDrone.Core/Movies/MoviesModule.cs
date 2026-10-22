using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Jobs;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.Commands;
using NzbDrone.Core.Movies.ImportLists;
using NzbDrone.Core.Movies.MediaFiles.Commands;

namespace NzbDrone.Core.Movies
{
    public class MoviesModule : IMediaModule
    {
        public static readonly IReadOnlyList<int> Categories = new[] { NewznabStandardCategory.Movies.Id };

        public MediaKind Kind => MediaKind.Movies;

        public string Name => "Movies";

        public IReadOnlyList<int> DefaultCategories => Categories;

        // Radarr's intervals.
        public IEnumerable<ScheduledTask> ScheduledTasks => new[]
        {
            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(RefreshMovieCommand).FullName
            },

            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(CleanUpRecycleBinCommand).FullName
            },

            new ScheduledTask
            {
                Interval = 24 * 60,
                TypeName = typeof(RefreshCollectionsCommand).FullName
            },

            new ScheduledTask
            {
                Interval = 5,
                TypeName = typeof(ImportListSyncCommand).FullName
            }
        };
    }
}
