using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Hygiene;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    // Run by name only. This is a profile of the production service path, not a timing assertion.
    [TestFixture]
    [Explicit]
    [NonParallelizable]
    public class HygieneQueueRefreshBenchmark : DbTest
    {
        private const int QueueSize = 20;

        [TestCase(100)]
        [TestCase(10000)]
        [TestCase(100000)]
        public void profile_actual_queue_refresh(int historySize)
        {
            if (Mocker.Resolve<IMainDatabase>().DatabaseType != DatabaseType.SQLite)
            {
                Assert.Ignore("This benchmark fixes SQLite as its database engine.");
            }

            var sourceDatabase = Mocker.Resolve<IDatabase>();
            var sqlTrace = new SqlTraceCapture();
            var tracedDatabase = new Database("Hygiene benchmark", () =>
            {
                var connection = (SQLiteConnection)sourceDatabase.OpenConnection();
                connection.TraceFlags = SQLiteTraceFlags.SQLITE_TRACE_STMT | SQLiteTraceFlags.SQLITE_TRACE_ROW;
                connection.Trace2 += sqlTrace.OnTrace;
                return connection;
            });
            Mocker.SetConstant<IDatabase>(tracedDatabase);
            Mocker.SetConstant<IMainDatabase>(new MainDatabase(tracedDatabase));

            var states = Mocker.Resolve<HygieneQueueStateRepository>();
            var actions = Mocker.Resolve<HygieneActionRepository>();
            var rules = Mocker.Resolve<HygieneRuleRepository>();
            Mocker.SetConstant<IHygieneQueueStateRepository>(states);
            Mocker.SetConstant<IHygieneActionRepository>(actions);
            Mocker.SetConstant<IHygieneRuleRepository>(rules);

            var config = Mocker.GetMock<IConfigService>();
            config.SetupGet(x => x.HygieneActionsEnabled).Returns(true);
            var evaluator = new HygieneQueueEvaluator();
            var ruleService = new HygieneRuleService(rules, TestLogger);
            var decisions = new HygieneQueueDecisionService(evaluator, states, ruleService, config.Object);
            var strikes = new HygieneQueueStrikeService(states, ruleService, evaluator);
            var actionService = new HygieneQueueActionService(
                decisions,
                actions,
                config.Object,
                Mocker.GetMock<ITrackedDownloadService>().Object,
                Mocker.GetMock<IProvideDownloadClient>().Object,
                Mocker.GetMock<IFailedDownloadService>().Object,
                Mocker.GetMock<NzbDrone.Core.Messaging.Commands.IManageCommandQueue>().Object,
                TestLogger);
            var timedStrikes = new TimedStrikes(strikes);
            var timedActions = new TimedActions(actionService);
            var queueService = new QueueService(Mocker.GetMock<IEventAggregator>().Object, timedStrikes, timedActions, TestLogger);
            var downloads = Enumerable.Range(0, QueueSize).Select(i => new TrackedDownload
            {
                DownloadClient = (i % 4) + 1,
                IsTrackable = true,
                Kind = MediaKind.Movies,
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem
                {
                    DownloadId = $"download-{i}",
                    Title = $"Synthetic {i}",
                    Status = DownloadItemStatus.Downloading,
                    TotalSize = 1000,
                    RemainingSize = 500,
                    DownloadClientInfo = new DownloadClientItemClientInfo { Name = "synthetic" }
                }
            }).ToList();

            var oldLogSql = SqlBuilderExtensions.LogSql;
            SqlBuilderExtensions.LogSql = false;
            try
            {
                // Warm the service/JIT path before filling the history and timing writes.
                queueService.Handle(new TrackedDownloadRefreshedEvent(new List<TrackedDownload>()));
                Seed(historySize);
                sqlTrace.Start();
                var first = Measure(queueService, downloads, timedStrikes, timedActions);
                var firstSql = sqlTrace.Stop();
                var steady = new List<(double ElapsedMs, double StrikesMs, double ActionsMs, long AllocatedBytes, long RssBytes)>();
                for (var repeat = 0; repeat < 5; repeat++)
                {
                    steady.Add(Measure(queueService, downloads, timedStrikes, timedActions));
                }

                using var connection = Mocker.Resolve<IMainDatabase>().OpenConnection();
                var stateCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM \"HygieneQueueStates\"");
                var actionCount = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM \"HygieneActions\"");
                var completedRecovery = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM \"HygieneActions\" WHERE \"DownloadId\" = 'recoverable' AND \"Status\" = 'completed'");
                stateCount.Should().Be(historySize - ((historySize - 1) / 100));
                actionCount.Should().Be(historySize);
                completedRecovery.Should().Be(1);
                queueService.GetQueue().Should().HaveCount(QueueSize);

                TestContext.Progress.WriteLine($"history={historySize} queue={QueueSize} first_ms={first.ElapsedMs:F3} first_strikes_ms={first.StrikesMs:F3} first_actions_ms={first.ActionsMs:F3} first_alloc_bytes={first.AllocatedBytes} first_rss_delta_bytes={first.RssBytes} first_sql_statements={firstSql.Statements} first_sql_rows={firstSql.Rows} " +
                    $"steady_median_ms={Median(steady.Select(x => x.ElapsedMs)):F3} steady_range_ms={steady.Min(x => x.ElapsedMs):F3}-{steady.Max(x => x.ElapsedMs):F3} " +
                    $"steady_strikes_ms={Median(steady.Select(x => x.StrikesMs)):F3} steady_actions_ms={Median(steady.Select(x => x.ActionsMs)):F3} " +
                    $"steady_median_alloc_bytes={Median(steady.Select(x => (double)x.AllocatedBytes)):F0} steady_rss_delta_bytes={steady.Sum(x => x.RssBytes)} " +
                    $"states_after={stateCount} actions_after={actionCount} recovered={completedRecovery}");
            }
            finally
            {
                SqlBuilderExtensions.LogSql = oldLogSql;
            }
        }

        private void Seed(int count)
        {
            var now = DateTime.UtcNow;
            var stale = now.AddDays(-31);
            var recent = now.AddDays(-1);
            var retryAt = now.AddDays(1);
            using var connection = Mocker.Resolve<IMainDatabase>().OpenConnection();
            using var transaction = connection.BeginTransaction();
            connection.Execute("WITH RECURSIVE n(x) AS (SELECT 0 UNION ALL SELECT x + 1 FROM n WHERE x + 1 < @count) " +
                "INSERT INTO \"HygieneQueueStates\" (\"DownloadClientId\", \"DownloadId\", \"MediaKind\", \"ReasonCode\", \"Strikes\", \"RemainingSize\", \"LastSeenAt\") " +
                "SELECT x % 4 + 1, printf('download-%d', x), 1, 'healthy', 0, 500, CASE WHEN x < 20 OR x % 100 = 0 THEN @stale ELSE @recent END FROM n",
                new { count, stale, recent },
                transaction);
            connection.Execute("WITH RECURSIVE n(x) AS (SELECT 0 UNION ALL SELECT x + 1 FROM n WHERE x + 1 < @count) " +
                "INSERT INTO \"HygieneActions\" (\"DownloadClientId\", \"DownloadId\", \"MediaKind\", \"ReasonCode\", \"RuleId\", \"Status\", \"Checkpoint\", \"RecoveryPath\", \"NextAttemptAt\", \"CreatedAt\", \"UpdatedAt\") " +
                "SELECT x % 4 + 1, CASE WHEN x = @last THEN 'recoverable' ELSE printf('download-%d', x % 2000) END, 1, 'client-failed', 1, " +
                "CASE WHEN x = @last THEN 'processing' WHEN x % 1000 = 999 THEN 'failed' ELSE 'completed' END, 'replacement-requested', 'synthetic', " +
                "CASE WHEN x % 1000 = 999 AND x != @last THEN @retryAt ELSE NULL END, @recent, @recent FROM n",
                new { count, last = count - 1, retryAt, recent },
                transaction);
            transaction.Commit();
        }

        private static (double ElapsedMs, double StrikesMs, double ActionsMs, long AllocatedBytes, long RssBytes) Measure(
            QueueService service, List<TrackedDownload> downloads, TimedStrikes strikes, TimedActions actions)
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var rss = process.WorkingSet64;
            var allocated = GC.GetTotalAllocatedBytes(true);
            var watch = Stopwatch.StartNew();
            service.Handle(new TrackedDownloadRefreshedEvent(downloads));
            watch.Stop();
            process.Refresh();
            return (watch.Elapsed.TotalMilliseconds, strikes.ElapsedMs, actions.ElapsedMs,
                GC.GetTotalAllocatedBytes(true) - allocated, process.WorkingSet64 - rss);
        }

        private sealed class TimedStrikes : IHygieneQueueStrikeService
        {
            private readonly IHygieneQueueStrikeService _inner;
            public double ElapsedMs { get; private set; }

            public TimedStrikes(IHygieneQueueStrikeService inner) => _inner = inner;

            public void Observe(IEnumerable<NzbDrone.Core.Queue.Queue> queue)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    _inner.Observe(queue);
                }
                finally
                {
                    ElapsedMs = watch.Elapsed.TotalMilliseconds;
                }
            }
        }

        private sealed class TimedActions : IHygieneQueueActionService
        {
            private readonly IHygieneQueueActionService _inner;
            public double ElapsedMs { get; private set; }

            public TimedActions(IHygieneQueueActionService inner) => _inner = inner;

            public void Process(IEnumerable<NzbDrone.Core.Queue.Queue> queue)
            {
                var watch = Stopwatch.StartNew();
                try
                {
                    _inner.Process(queue);
                }
                finally
                {
                    ElapsedMs = watch.Elapsed.TotalMilliseconds;
                }
            }

            public List<HygieneAction> Recent(int limit = 100) => _inner.Recent(limit);
        }

        private static double Median(IEnumerable<double> values)
        {
            var ordered = values.OrderBy(x => x).ToArray();
            return ordered[ordered.Length / 2];
        }

        private sealed class SqlTraceCapture
        {
            private bool _enabled;
            private int _statements;
            private int _rows;

            public void Start()
            {
                _statements = 0;
                _rows = 0;
                _enabled = true;
            }

            public (int Statements, int Rows) Stop()
            {
                _enabled = false;
                return (_statements, _rows);
            }

            public void OnTrace(object sender, TraceEventArgs args)
            {
                if (!_enabled)
                {
                    return;
                }

                if ((args.Flags & SQLiteTraceFlags.SQLITE_TRACE_STMT) != 0)
                {
                    _statements++;
                }

                if ((args.Flags & SQLiteTraceFlags.SQLITE_TRACE_ROW) != 0)
                {
                    _rows++;
                }
            }
        }
    }
}
