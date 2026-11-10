using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Core.Backup;
using NzbDrone.Core.Caching;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Configuration.Events;
using NzbDrone.Core.Download;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.IndexerHistory;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.IndexerVersions;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Update.Commands;

namespace NzbDrone.Core.Jobs
{
    public interface ITaskManager
    {
        IList<ScheduledTask> GetPending();
        List<ScheduledTask> GetAll();
        DateTime GetNextExecution(Type type);
    }

    public class TaskManager : ITaskManager, IHandle<ApplicationStartedEvent>, IHandle<CommandExecutedEvent>, IHandleAsync<ConfigSavedEvent>
    {
        private readonly IScheduledTaskRepository _scheduledTaskRepository;
        private readonly IConfigService _configService;
        private readonly List<IMediaModule> _modules;
        private readonly Logger _logger;
        private readonly ICached<ScheduledTask> _cache;

        public TaskManager(IScheduledTaskRepository scheduledTaskRepository,
                           IConfigService configService,
                           IEnumerable<IMediaModule> modules,
                           ICacheManager cacheManager,
                           Logger logger)
        {
            _scheduledTaskRepository = scheduledTaskRepository;
            _configService = configService;
            _modules = modules.ToList();
            _cache = cacheManager.GetCache<ScheduledTask>(GetType());
            _logger = logger;
        }

        public IList<ScheduledTask> GetPending()
        {
            return _cache.Values
                         .Where(c => c.Interval > 0 && c.LastExecution.AddMinutes(c.Interval) < DateTime.UtcNow)
                         .ToList();
        }

        public List<ScheduledTask> GetAll()
        {
            return _cache.Values.ToList();
        }

        public DateTime GetNextExecution(Type type)
        {
            var scheduledTask = _cache.Find(type.FullName);

            return scheduledTask.LastExecution.AddMinutes(scheduledTask.Interval);
        }

        public void Handle(ApplicationStartedEvent message)
        {
            var defaultTasks = new List<ScheduledTask>
                {
                    new ScheduledTask
                    {
                        Interval = 5,
                        TypeName = typeof(MessagingCleanupCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 5,
                        TypeName = typeof(ClearExpiredCacheCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 6 * 60,
                        TypeName = typeof(ApplicationCheckUpdateCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 6 * 60,
                        TypeName = typeof(CheckHealthCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 5,
                        TypeName = typeof(CheckIndexerStatusHealthCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 24 * 60,
                        TypeName = typeof(HousekeepingCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 60,
                        TypeName = typeof(HygieneBlacklistSyncCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 12 * 60,
                        TypeName = typeof(BackfillMediaCoversCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 24 * 60,
                        TypeName = typeof(CleanUpIndexerHistoryCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = 24 * 60,
                        TypeName = typeof(IndexerDefinitionUpdateCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = GetBackupInterval(),
                        TypeName = typeof(BackupCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = GetRssSyncInterval(),
                        TypeName = typeof(RssSyncCommand).FullName
                    },

                    new ScheduledTask
                    {
                        Interval = GetRefreshMonitoredInterval(),
                        TypeName = typeof(RefreshMonitoredDownloadsCommand).FullName,
                        Priority = CommandPriority.High
                    }
                };

            defaultTasks.AddRange(_modules.SelectMany(m => m.ScheduledTasks));

            foreach (var interval in ConfiguredIntervals())
            {
                var task = defaultTasks.FirstOrDefault(item => item.TypeName == interval.Key);

                if (task != null)
                {
                    task.Interval = SearchInterval(interval.Value);
                }
            }

            var currentTasks = _scheduledTaskRepository.All().ToList();

            _logger.Trace("Initializing jobs. Available: {0} Existing: {1}", defaultTasks.Count, currentTasks.Count);

            foreach (var job in currentTasks)
            {
                if (!defaultTasks.Any(c => c.TypeName == job.TypeName))
                {
                    _logger.Trace("Removing job from database '{0}'", job.TypeName);
                    _scheduledTaskRepository.Delete(job.Id);
                }
            }

            foreach (var defaultTask in defaultTasks)
            {
                var currentDefinition = currentTasks.SingleOrDefault(c => c.TypeName == defaultTask.TypeName) ?? defaultTask;

                currentDefinition.Interval = defaultTask.Interval;

                if (currentDefinition.Id == 0)
                {
                    currentDefinition.LastExecution = DateTime.UtcNow;
                }

                currentDefinition.Priority = defaultTask.Priority;

                _cache.Set(currentDefinition.TypeName, currentDefinition);
                _scheduledTaskRepository.Upsert(currentDefinition);
            }
        }

        private int GetBackupInterval()
        {
            var intervalDays = _configService.BackupInterval;

            if (intervalDays < 1)
            {
                intervalDays = 1;
            }

            if (intervalDays > 7)
            {
                intervalDays = 7;
            }

            return intervalDays * 60 * 24;
        }

        private static int SearchInterval(int interval)
        {
            return interval >= 5 && interval <= 1440 ? interval : 60;
        }

        private Dictionary<string, int> ConfiguredIntervals()
        {
            return _modules.OfType<IConfiguredSchedule>()
                .SelectMany(module => module.ConfiguredIntervals(_configService))
                .ToDictionary(item => item.Key, item => item.Value);
        }

        private int GetRssSyncInterval()
        {
            var interval = _configService.RssSyncInterval;

            if (interval > 0 && interval < 10)
            {
                return 10;
            }

            if (interval < 0)
            {
                return 0;
            }

            return interval;
        }

        private int GetRefreshMonitoredInterval()
        {
            var interval = _configService.CheckForFinishedDownloadInterval;

            if (interval < 1)
            {
                return 1;
            }

            return interval;
        }

        public void Handle(CommandExecutedEvent message)
        {
            var scheduledTask = _scheduledTaskRepository.All().SingleOrDefault(c => c.TypeName == message.Command.Body.GetType().FullName);

            if (scheduledTask != null && message.Command.Body.UpdateScheduledTask)
            {
                _logger.Trace("Updating last run time for: {0}", scheduledTask.TypeName);

                var lastExecution = DateTime.UtcNow;

                _scheduledTaskRepository.SetLastExecutionTime(scheduledTask.Id, lastExecution, message.Command.StartedAt.Value);
                _cache.Find(scheduledTask.TypeName).LastExecution = lastExecution;
                _cache.Find(scheduledTask.TypeName).LastStartTime = message.Command.StartedAt.Value;
            }
        }

        public void HandleAsync(ConfigSavedEvent message)
        {
            var rss = _scheduledTaskRepository.GetDefinition(typeof(RssSyncCommand));
            rss.Interval = GetRssSyncInterval();

            var backup = _scheduledTaskRepository.GetDefinition(typeof(BackupCommand));
            backup.Interval = GetBackupInterval();

            var refreshMonitoredDownloads = _scheduledTaskRepository.GetDefinition(typeof(RefreshMonitoredDownloadsCommand));
            refreshMonitoredDownloads.Interval = GetRefreshMonitoredInterval();

            var tasks = new List<ScheduledTask> { rss, refreshMonitoredDownloads, backup };
            foreach (var interval in ConfiguredIntervals())
            {
                var task = _scheduledTaskRepository.All().FirstOrDefault(item => item.TypeName == interval.Key);

                if (task != null)
                {
                    task.Interval = SearchInterval(interval.Value);
                    tasks.Add(task);
                }
            }

            _scheduledTaskRepository.UpdateMany(tasks);

            _cache.Find(rss.TypeName).Interval = rss.Interval;
            _cache.Find(backup.TypeName).Interval = backup.Interval;
            _cache.Find(refreshMonitoredDownloads.TypeName).Interval = refreshMonitoredDownloads.Interval;
            foreach (var task in tasks.Skip(3))
            {
                _cache.Find(task.TypeName).Interval = task.Interval;
            }
        }
    }
}
