using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The /import command around the importer: copying, reading, the refusal to commit a partial import,
    // exit codes, and the report files. The importer is a mock; its own behavior is UpstreamImporterFixture.
    [TestFixture]
    public class UpstreamImportRunnerFixture : CoreTest<UpstreamImportRunner>
    {
        private string _source;
        private string _reportDirectory;
        private string _scratch;
        private Mock<IUpstreamImporter> _importer;
        private ImportOutcome? _outcome;
        private IReadOnlyList<SourceSnapshot> _snapshots;
        private ImportOptions _options;
        private bool? _dryRun;
        private IdMap _idMap;

        [SetUp]
        public void Setup()
        {
            _reportDirectory = Path.Combine(TempFolder, "report");
            _scratch = Path.Combine(TempFolder, "scratch");
            _outcome = null;
            _snapshots = null;
            _options = null;
            _dryRun = null;
            _idMap = new IdMap();

            Mocker.GetMock<IMediaCoverBackfillService>()
                .Setup(c => c.Backfill(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>()))
                .Returns(new CoverBackfillResult());

            Mocker.SetConstant<IEnumerable<IUpstreamAdapter>>(new IUpstreamAdapter[] { new RadarrAdapter(), new ProwlarrAdapter() });

            // One stage that claims Radarr Tags, so any other table with rows is "not yet imported".
            var stage = new Mock<IImportStage>();
            stage.Setup(s => s.Tables).Returns(new[] { new SourceTableRef("Radarr", "Tags") });
            Mocker.SetConstant<IEnumerable<IImportStage>>(new[] { stage.Object });

            _importer = Mocker.GetMock<IUpstreamImporter>();
            _importer
                .Setup(i => i.Run(It.IsAny<IReadOnlyList<SourceSnapshot>>(), It.IsAny<ImportOptions>(), It.IsAny<bool>()))
                .Returns((IReadOnlyList<SourceSnapshot> snapshots, ImportOptions options, bool dryRun) =>
                {
                    _snapshots = snapshots;
                    _options = options;
                    _dryRun = dryRun;

                    return new ImportResult
                    {
                        Outcome = _outcome ?? (dryRun ? ImportOutcome.DryRun : ImportOutcome.Committed),
                        DeferredTables = options.SkipTables.Select(selection => new DeferredSourceTable
                        {
                            App = selection.App,
                            Table = selection.Table,
                            Rows = snapshots.Single(s => s.AppName == selection.App).Rows(selection.Table).Count
                        }).ToList(),
                        Tables = options.SkipTables.Select(selection =>
                        {
                            var rows = snapshots.Single(s => s.AppName == selection.App).Rows(selection.Table).Count;

                            return new ReconciliationRow(selection.App, selection.Table, rows, 0, rows);
                        }).ToList(),
                        IdMap = _idMap
                    };
                });

            _source = CreateRadarrDatabase(UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'"));
        }

        private string CreateRadarrDatabase(params string[] rows)
        {
            var path = Path.Combine(TempFolder, "radarr.db");

            File.Delete(path);

            UpstreamTestDatabase.CreateFromSchema(
                path,
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                new[] { "INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES (242, '2026-10-21 00:00:00', 'test')" }.Concat(rows).ToArray());

            return path;
        }

        private ImportRequest Request(bool dryRun = false)
        {
            return new ImportRequest
            {
                RadarrPath = _source,
                DryRun = dryRun,
                ReportDirectory = _reportDirectory,
                ScratchDirectory = _scratch
            };
        }

        private static string Hash(string path)
        {
            return Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        }

        [Test]
        public void should_read_the_source_and_hand_the_importer_a_snapshot_and_the_dry_run_flag()
        {
            var result = Subject.Run(Request(true));

            result.ExitCode.Should().Be(0);
            _dryRun.Should().BeTrue();
            _snapshots.Should().ContainSingle().Which.Version.Should().Be(242);
            _snapshots.Single().AppName.Should().Be("Radarr");
            _snapshots.Single().Rows("Tags").Should().HaveCount(1);
        }

        [Test]
        public void should_write_the_report_files_and_name_the_source_by_file_name()
        {
            var result = Subject.Run(Request(true));

            result.ReportFiles.Select(Path.GetExtension).Should().BeEquivalentTo(".json", ".txt");
            result.ReportFiles.Should().OnlyContain(f => File.Exists(f));
            result.Report.Sources.Should().ContainSingle().Which.File.Should().Be("radarr.db");
            result.Report.Sources.Single().Sha256.Should().Be(Hash(_source).ToLowerInvariant());
        }

        [Test]
        public void should_not_modify_the_source_and_should_remove_its_copy()
        {
            var before = Hash(_source);

            Subject.Run(Request(true));

            Hash(_source).Should().Be(before);
            Directory.GetFileSystemEntries(_scratch).Should().BeEmpty();
        }

        [Test]
        public void should_commit_a_real_run_when_every_table_with_rows_has_a_stage_and_write_the_id_map()
        {
            var result = Subject.Run(Request());

            result.ExitCode.Should().Be(0);
            _dryRun.Should().BeFalse();
            result.Report.Outcome.Should().Be("Committed");
            result.ReportFiles.Select(Path.GetFileName).Should().Contain(f => f.StartsWith("import-idmap-"));
        }

        [Test]
        public void should_backfill_only_committed_movie_and_tv_ids_after_import()
        {
            _idMap.Record(UpstreamApps.Radarr, "Movies", 9, 2);
            _idMap.Record(UpstreamApps.Sonarr, "Series", 8, 2);
            _idMap.Record(UpstreamApps.Radarr, "Tags", 1, 1);

            Subject.Run(Request()).ExitCode.Should().Be(0);

            Mocker.GetMock<IMediaCoverBackfillService>()
                .Verify(c => c.Backfill(
                    It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 2 })),
                    It.Is<IEnumerable<int>>(ids => ids.SequenceEqual(new[] { 2 }))),
                    Times.Once());
        }

        [Test]
        public void should_not_backfill_a_dry_run_or_strict_skip()
        {
            Subject.Run(Request(true)).ExitCode.Should().Be(0);

            _outcome = ImportOutcome.SkippedRows;
            Subject.Run(Request()).ExitCode.Should().Be(2);

            Mocker.GetMock<IMediaCoverBackfillService>()
                .Verify(c => c.Backfill(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>()), Times.Never());
        }

        [Test]
        public void should_report_missing_covers_without_claiming_a_committed_import_rolled_back()
        {
            Mocker.GetMock<IMediaCoverBackfillService>()
                .Setup(c => c.Backfill(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>()))
                .Returns(new CoverBackfillResult { Checked = 2, Missing = 1 });

            var result = Subject.Run(Request());

            result.ExitCode.Should().Be(0);
            result.Report.Outcome.Should().Be("Committed");
            result.Report.Warnings.Should().ContainSingle(w => w.Contains("1 of 2"));
        }

        [Test]
        public void should_write_the_committed_report_before_cover_backfill_and_clear_pending_when_done()
        {
            Mocker.GetMock<IMediaCoverBackfillService>()
                .Setup(c => c.Backfill(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>()))
                .Returns(() =>
                {
                    Directory.GetFiles(_reportDirectory, "import-*.json")
                        .Should().ContainSingle()
                        .Which.Should().Contain("import-");

                    File.ReadAllText(Directory.GetFiles(_reportDirectory, "import-*.json").Single())
                        .Should().Contain("Cover backfill pending").And.Contain("Committed");

                    return new CoverBackfillResult();
                });

            var result = Subject.Run(Request());

            result.Report.Warnings.Should().NotContain(w => w.Contains("Cover backfill pending"));
            File.ReadAllText(result.ReportFiles.Single(f => Path.GetExtension(f) == ".json"))
                .Should().NotContain("Cover backfill pending");
        }

        [Test]
        public void should_keep_the_committed_outcome_and_report_a_backfill_exception()
        {
            Mocker.GetMock<IMediaCoverBackfillService>()
                .Setup(c => c.Backfill(It.IsAny<IEnumerable<int>>(), It.IsAny<IEnumerable<int>>()))
                .Throws(new InvalidOperationException("download failed"));

            var result = Subject.Run(Request());

            result.ExitCode.Should().Be(0);
            result.Report.Outcome.Should().Be("Committed");
            result.Report.Warnings.Should().ContainSingle(w => w.Contains("did not complete"));
            File.ReadAllText(result.ReportFiles.Single(f => Path.GetExtension(f) == ".json"))
                .Should().Contain("did not complete").And.NotContain("Cover backfill pending");
        }

        [Test]
        public void should_refuse_a_real_run_while_a_table_with_rows_has_no_stage()
        {
            _source = CreateRadarrDatabase(
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'"),
                UpstreamFixtureTest.Insert("Config", "Key, Value", "'x', 'y'"));

            var result = Subject.Run(Request());

            result.ExitCode.Should().Be(1);
            result.Report.Should().BeNull();
            result.Error.Should().Contain("Radarr:Config").And.Contain("/skip=");
            _importer.Verify(i => i.Run(It.IsAny<IReadOnlyList<SourceSnapshot>>(), It.IsAny<ImportOptions>(), It.IsAny<bool>()), Times.Never());
            Directory.GetFileSystemEntries(_scratch).Should().BeEmpty();
        }

        [Test]
        public void should_run_a_dry_run_past_a_table_with_no_stage_and_report_it()
        {
            _source = CreateRadarrDatabase(UpstreamFixtureTest.Insert("Config", "Key, Value", "'x', 'y'"));

            var request = Request(true);
            request.SkipTables.Add(new SourceTableRef("Radarr", "Config"));

            var result = Subject.Run(request);

            result.ExitCode.Should().Be(0);
            result.Report.DeferredByOwner.Should().ContainSingle().Which.Should().BeEquivalentTo(new DeferredSourceTable { App = "Radarr", Table = "Config", Rows = 1 });
            result.Report.Tables.Should().Contain(t => t.App == "Radarr" && t.Table == "Config" && t.Read == 1 && t.Imported == 0 && t.Skipped == 1);
        }

        [Test]
        public void should_exit_with_2_when_strict_mode_stopped_the_run_and_still_write_the_report()
        {
            _outcome = ImportOutcome.SkippedRows;

            var result = Subject.Run(Request());

            result.ExitCode.Should().Be(2);
            result.ReportFiles.Should().NotBeEmpty();
        }

        [Test]
        public void should_fail_with_the_file_name_when_the_source_is_missing()
        {
            var request = Request();

            request.RadarrPath = Path.Combine(TempFolder, "nope.db");

            var result = Subject.Run(request);

            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("nope.db");
        }

        [Test]
        public void should_pass_the_options_file_to_the_adapter_and_importer_with_names_in_any_case()
        {
            var options = Path.Combine(TempFolder, "options.json");

            File.WriteAllText(options, "{ \"PathMap\": { \"/movies\": \"/data/media/movies\" }, \"strict\": true, // comment\n \"allowNewerSource\": true }");

            var request = Request(true);

            request.OptionsPath = options;

            Subject.Run(request).ExitCode.Should().Be(0);

            _options.Strict.Should().BeTrue();
            _options.AllowNewerSource.Should().BeTrue();
            _options.PathMap.Should().ContainKey("/movies").WhoseValue.Should().Be("/data/media/movies");
        }

        [Test]
        public void should_fail_on_an_options_file_that_is_not_json_without_reading_the_sources()
        {
            var options = Path.Combine(TempFolder, "options.json");

            File.WriteAllText(options, "{ not json");

            var request = Request(true);

            request.OptionsPath = options;

            var result = Subject.Run(request);

            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("options.json");
            _snapshots.Should().BeNull();
        }

        [Test]
        public void should_fail_when_the_source_is_newer_than_the_adapter_supports()
        {
            _source = CreateRadarrDatabase("INSERT INTO VersionInfo (Version, AppliedOn, Description) VALUES (9999, '2026-10-21 00:00:00', 'newer')");

            var result = Subject.Run(Request(true));

            result.ExitCode.Should().Be(1);
            result.Error.Should().Contain("9999");
        }
    }
}
