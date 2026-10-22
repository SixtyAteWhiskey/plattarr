using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Movies.Upstream;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The engine and its first two stages against a migrated target (SQLite, and Postgres when
    // the test run is pointed at one), with source rows read back through the real adapters from
    // the real schemas.
    [TestFixture]
    public class UpstreamImporterFixture : DbTest
    {
        private TagRepository _tagRepository;
        private RootFolderRepository _rootFolderRepository;

        [SetUp]
        public void SetUpStages()
        {
            _tagRepository = Mocker.Resolve<TagRepository>();
            _rootFolderRepository = Mocker.Resolve<RootFolderRepository>();

            Mocker.SetConstant<ITagRepository>(_tagRepository);
            Mocker.SetConstant<IRootFolderRepository>(_rootFolderRepository);

            UseStages(Mocker.Resolve<TagsStage>(), Mocker.Resolve<RootFoldersStage>());
        }

        private void UseStages(params IImportStage[] stages)
        {
            Mocker.SetConstant<IEnumerable<IImportStage>>(stages);
        }

        private SourceSnapshot RadarrSnapshot(params string[] extraRows)
        {
            return UpstreamFixtureTest.ReadSnapshot(
                new RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                UpstreamFixtureTest.RadarrRows.Concat(extraRows),
                TempFolder);
        }

        private SourceSnapshot ProwlarrSnapshot(params string[] extraRows)
        {
            return UpstreamFixtureTest.ReadSnapshot(
                new ProwlarrAdapter(),
                GetTestPath("Files/Upstream/prowlarr-44.schema.sql"),
                44,
                UpstreamFixtureTest.ProwlarrRows.Concat(extraRows),
                TempFolder);
        }

        private ImportResult Run(ImportOptions options, bool dryRun, params SourceSnapshot[] snapshots)
        {
            return Mocker.Resolve<UpstreamImporter>().Run(snapshots, options, dryRun);
        }

        private static string RootFolderRow(int id, string path)
        {
            return UpstreamFixtureTest.Insert("RootFolders", "Id, Path", $"{id}, {UpstreamFixtureTest.Sql(path)}");
        }

        [Test]
        public void should_write_the_union_of_tags_merged_by_label()
        {
            // Radarr has Kids; Prowlarr has flaresolverr and kids.
            var result = Run(new ImportOptions(), false, RadarrSnapshot(), ProwlarrSnapshot());

            result.Outcome.Should().Be(ImportOutcome.Committed);
            _tagRepository.All().Select(t => t.Label).Should().BeEquivalentTo("Kids", "flaresolverr");

            var kids = _tagRepository.All().Single(t => t.Label == "Kids").Id;

            result.IdMap.Resolve("Radarr", "Tags", 1).Should().Be(kids);
            result.IdMap.Resolve("Prowlarr", "Tags", 2).Should().Be(kids);
            result.IdMap.Resolve("Prowlarr", "Tags", 1).Should().NotBe(kids);
        }

        [Test]
        public void should_account_for_every_row_of_the_tables_the_stages_claim()
        {
            var result = Run(new ImportOptions(), false, RadarrSnapshot(RootFolderRow(1, "/movies")), ProwlarrSnapshot());

            result.Tables.Should().BeEquivalentTo(new[]
            {
                new { App = "Prowlarr", Table = "Tags", Read = 2L, Imported = 2L, Skipped = 0L },
                new { App = "Radarr", Table = "RootFolders", Read = 1L, Imported = 1L, Skipped = 0L },
                new { App = "Radarr", Table = "Tags", Read = 1L, Imported = 1L, Skipped = 0L }
            });
        }

        [Test]
        public void should_report_the_tables_that_are_counted_and_not_read()
        {
            var result = Run(new ImportOptions(), false, RadarrSnapshot(), ProwlarrSnapshot());

            result.UnreadTables.Should().Contain(t => t.App == "Prowlarr" && t.Name == "Config" && t.Reason == SkipReason.Instance);
            result.UnreadTables.Should().Contain(t => t.App == "Radarr" && t.Name == "PendingReleases" && t.Reason == SkipReason.Transient);
        }

        [Test]
        public void should_apply_the_path_map_to_root_folders()
        {
            var options = new ImportOptions();
            options.PathMap["/movies"] = "/data/media/movies";

            var result = Run(options, false, RadarrSnapshot(RootFolderRow(1, "/movies"), RootFolderRow(2, "/movies/4k"), RootFolderRow(3, "/elsewhere")));

            _rootFolderRepository.All().Select(r => r.Path)
                .Should().BeEquivalentTo("/data/media/movies", "/data/media/movies/4k", "/elsewhere");
            result.IdMap.Resolve("Radarr", "RootFolders", 2).Should().BePositive();
        }

        [Test]
        public void should_write_one_root_folder_when_two_map_onto_the_same_path()
        {
            var options = new ImportOptions();
            options.PathMap["/a"] = "/data";
            options.PathMap["/b"] = "/data";

            var result = Run(options, false, RadarrSnapshot(RootFolderRow(1, "/a"), RootFolderRow(2, "/b")));

            _rootFolderRepository.All().Should().ContainSingle(r => r.Path == "/data");
            result.IdMap.Resolve("Radarr", "RootFolders", 1).Should().Be(result.IdMap.Resolve("Radarr", "RootFolders", 2));
            result.Tables.Single(t => t.Table == "RootFolders").Balanced.Should().BeTrue();
        }

        [Test]
        public void should_skip_a_root_folder_without_a_path_and_still_balance()
        {
            var result = Run(new ImportOptions(), false, RadarrSnapshot(RootFolderRow(1, "  ")));

            result.Skips.Should().ContainSingle(s => s.Table == "RootFolders" && s.Reason == SkipReason.Corrupt && s.SourceId == 1);
            result.Tables.Single(t => t.Table == "RootFolders").Balanced.Should().BeTrue();
            _rootFolderRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_write_nothing_for_a_dry_run()
        {
            var result = Run(new ImportOptions(), true, RadarrSnapshot(RootFolderRow(1, "/movies")), ProwlarrSnapshot());

            result.Outcome.Should().Be(ImportOutcome.DryRun);
            result.Tables.Should().NotBeEmpty();
            _tagRepository.All().Should().BeEmpty();
            _rootFolderRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_write_nothing_in_strict_mode_when_a_row_was_skipped()
        {
            var result = Run(new ImportOptions { Strict = true }, false, RadarrSnapshot(RootFolderRow(1, "  ")));

            result.Outcome.Should().Be(ImportOutcome.SkippedRows);
            result.Skips.Should().HaveCount(1);
            _tagRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_commit_in_strict_mode_when_nothing_was_skipped()
        {
            var result = Run(new ImportOptions { Strict = true }, false, RadarrSnapshot());

            result.Outcome.Should().Be(ImportOutcome.Committed);
            _tagRepository.All().Should().ContainSingle(t => t.Label == "Kids");
        }

        [Test]
        public void should_roll_back_earlier_stages_when_a_later_stage_fails()
        {
            UseStages(Mocker.Resolve<TagsStage>(), new ThrowingStage(ImportOrder.RootFolders));

            Action run = () => Run(new ImportOptions(), false, RadarrSnapshot());

            run.Should().Throw<InvalidOperationException>().WithMessage("stage failed");
            _tagRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_run_the_stages_in_order_whatever_the_registration_order()
        {
            var ran = new List<string>();

            UseStages(new RecordingStage("late", 900, ran), new RecordingStage("early", 100, ran), new RecordingStage("middle", 500, ran));

            Run(new ImportOptions(), true, RadarrSnapshot());

            ran.Should().Equal("early", "middle", "late");
        }

        [Test]
        public void should_fail_when_a_stage_loses_rows()
        {
            UseStages(Mocker.Resolve<TagsStage>(), new LossyStage());

            Action run = () => Run(new ImportOptions(), false, RadarrSnapshot(RootFolderRow(1, "/movies")));

            var failure = run.Should().Throw<ImportVerificationException>().Which;

            failure.Unbalanced.Should().ContainSingle(r => r.App == "Radarr" && r.Table == "RootFolders" && r.Read == 1 && r.Imported == 0);
            failure.Message.Should().Contain("Radarr RootFolders").And.NotContain("/movies");
            _tagRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_refuse_a_target_that_already_holds_data()
        {
            _tagRepository.Insert(new Tag { Label = "existing" });

            Action run = () => Run(new ImportOptions(), false, RadarrSnapshot());

            run.Should().Throw<UpstreamImportException>().WithMessage("*table Tags*");
        }

        [Test]
        public void should_refuse_a_target_that_recorded_an_earlier_import()
        {
            Mocker.Resolve<ConfigRepository>().Insert(new Config { Key = "importedfrom", Value = "earlier" });

            Action run = () => Run(new ImportOptions(), false, RadarrSnapshot());

            run.Should().Throw<UpstreamImportException>().WithMessage("*already holds an import*");
            _tagRepository.All().Should().BeEmpty();
        }

        [Test]
        public void should_refuse_two_databases_of_the_same_app()
        {
            var radarr = RadarrSnapshot();

            Action run = () => Run(new ImportOptions(), false, radarr, radarr);

            run.Should().Throw<UpstreamImportException>().WithMessage("*Two Radarr databases*");
        }

        private class RecordingStage : IImportStage
        {
            private readonly List<string> _ran;

            public RecordingStage(string name, int order, List<string> ran)
            {
                Name = name;
                Order = order;
                _ran = ran;
            }

            public string Name { get; }
            public int Order { get; }
            public IReadOnlyList<SourceTableRef> Tables => Array.Empty<SourceTableRef>();

            public void Run(ImportSession session)
            {
                _ran.Add(Name);
            }
        }

        private class ThrowingStage : IImportStage
        {
            public ThrowingStage(int order)
            {
                Order = order;
            }

            public string Name => "throws";
            public int Order { get; }
            public IReadOnlyList<SourceTableRef> Tables => Array.Empty<SourceTableRef>();

            public void Run(ImportSession session)
            {
                throw new InvalidOperationException("stage failed");
            }
        }

        // Claims a table and then imports none of its rows.
        private class LossyStage : IImportStage
        {
            public string Name => "lossy";
            public int Order => ImportOrder.RootFolders;
            public IReadOnlyList<SourceTableRef> Tables => new[] { new SourceTableRef("Radarr", "RootFolders") };

            public void Run(ImportSession session)
            {
            }
        }
    }
}
