using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class ImportReportWriterFixture : CoreTest
    {
        private static readonly DateTime At = new DateTime(2026, 10, 22, 17, 30, 5, DateTimeKind.Utc);

        private static ImportResult Result(ImportOutcome outcome)
        {
            var map = new IdMap();

            map.Record("Radarr", "Movies", 7, 1);
            map.Record("Radarr", "Movies", 3, 2);
            map.Record("Radarr", "Tags", 1, 1);

            return new ImportResult
            {
                Outcome = outcome,
                IdMap = map,
                Tables = new List<ReconciliationRow>
                {
                    new ReconciliationRow("Radarr", "Movies", 87, 85, 2),
                    new ReconciliationRow("Radarr", "Tags", 1, 1, 0)
                },
                Skips = new List<ImportSkip>
                {
                    new ImportSkip("Radarr", "Movies", 12, "Sintel", SkipReason.Corrupt, "quality profile was not imported"),
                    new ImportSkip("Radarr", "Movies", 15, "Big Buck Bunny", SkipReason.Corrupt, "path is empty")
                },
                UnreadTables = new List<UnreadTable> { new UnreadTable("Radarr", new SkippedTable("Commands", 4, SkipReason.Transient)) },
                Warnings = new List<string> { "a warning" }
            };
        }

        private static ImportReport Report(ImportOutcome outcome, params DeferredSourceTable[] deferred)
        {
            var sources = new[] { new ReportSource { App = "Radarr", File = "radarr.db", Version = 242, Sha256 = "abc123" } };
            var result = Result(outcome);
            result.DeferredTables = deferred.ToList();
            result.Tables.AddRange(deferred.Select(t => new ReconciliationRow(t.App, t.Table, t.Rows, 0, t.Rows)));

            return ImportReportWriter.Build(result, sources, outcome == ImportOutcome.DryRun, At);
        }

        [Test]
        public void should_count_skips_by_reason_per_table()
        {
            var report = Report(ImportOutcome.DryRun);

            report.Tables.Single(t => t.Table == "Movies").Reasons.Should().BeEquivalentTo(new Dictionary<string, long> { ["CORRUPT"] = 2 });
            report.Tables.Single(t => t.Table == "Tags").Reasons.Should().BeEmpty();
        }

        [Test]
        public void should_keep_titles_out_of_the_console_summary_but_in_the_text_report()
        {
            var report = Report(ImportOutcome.DryRun);

            var summary = ImportReportWriter.ToSummary(report);
            var text = ImportReportWriter.ToText(report);

            summary.Should().Contain("Radarr Movies: read 87, imported 85, skipped 2 (CORRUPT 2)");
            summary.Should().NotContain("Sintel").And.NotContain("Big Buck Bunny");
            text.Should().Contain("Radarr Movies #12 \"Sintel\": CORRUPT: quality profile was not imported");
        }

        [Test]
        public void should_say_up_front_what_needs_attention()
        {
            var text = ImportReportWriter.ToText(Report(ImportOutcome.DryRun, new DeferredSourceTable { App = "Radarr", Table = "Indexers", Rows = 17 }));

            text.Should().Contain("Outcome: dry run (rolled back, nothing was written)");
            text.Should().Contain("2 rows were skipped (CORRUPT 2).");
            text.Should().Contain("1 source tables (17 rows) deferred by the owner with /skip=.");
            text.Should().Contain("Deferred by the owner with /skip=:");
            text.Should().Contain("Radarr Indexers: 17 rows");
            text.Should().Contain("Radarr Commands: 4 rows (TRANSIENT)");
        }

        [Test]
        public void should_write_the_source_file_name_and_hash_but_not_its_path()
        {
            var json = JsonDocument.Parse(ImportReportWriter.ToJson(Report(ImportOutcome.Committed)));
            var source = json.RootElement.GetProperty("sources")[0];

            source.GetProperty("file").GetString().Should().Be("radarr.db");
            source.GetProperty("sha256").GetString().Should().Be("abc123");
            json.RootElement.GetProperty("outcome").GetString().Should().Be("Committed");
        }

        [Test]
        public void should_list_the_id_map_in_a_stable_order()
        {
            ImportReportWriter.ToIdMapCsv(Result(ImportOutcome.Committed).IdMap).Should().Be(
                "app,table,sourceId,targetId" + Environment.NewLine
                + "Radarr,Movies,3,2" + Environment.NewLine
                + "Radarr,Movies,7,1" + Environment.NewLine
                + "Radarr,Tags,1,1" + Environment.NewLine);
        }

        [Test]
        public void should_write_the_id_map_only_for_a_committed_run()
        {
            var directory = Path.Combine(TempFolder, "report");

            var dry = ImportReportWriter.Write(directory, Report(ImportOutcome.DryRun), Result(ImportOutcome.DryRun));
            dry.Select(Path.GetFileName).Should().BeEquivalentTo("import-20261022-173005.json", "import-20261022-173005.txt");

            var committed = ImportReportWriter.Write(directory, Report(ImportOutcome.Committed), Result(ImportOutcome.Committed));
            committed.Select(Path.GetFileName).Should().BeEquivalentTo("import-20261022-173005.json", "import-20261022-173005.txt", "import-idmap-20261022-173005.csv");
        }
    }
}
