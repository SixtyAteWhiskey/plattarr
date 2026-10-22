using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class ImportReconciliationFixture : CoreTest
    {
        private static SourceSnapshot Snapshot(string app, string table, int rows)
        {
            var snapshot = new SourceSnapshot(app, 1);
            var sourceRows = new List<SourceRow>();

            for (var i = 1; i <= rows; i++)
            {
                sourceRows.Add(new SourceRow(new Dictionary<string, object> { ["Id"] = (long)i }));
            }

            snapshot.Add(new SourceTable(table, new[] { "Id" }, sourceRows));

            return snapshot;
        }

        private static ImportSession Session(params SourceSnapshot[] snapshots)
        {
            return new ImportSession((IDbConnection)null, null, snapshots, new ImportOptions());
        }

        private class Claims : IImportStage
        {
            public Claims(params SourceTableRef[] tables)
            {
                Tables = tables;
            }

            public string Name => "claims";
            public int Order => 0;
            public IReadOnlyList<SourceTableRef> Tables { get; }

            public void Run(ImportSession session)
            {
            }
        }

        [Test]
        public void should_balance_when_rows_are_imported_or_skipped()
        {
            var session = Session(Snapshot("Radarr", "Movies", 5));
            session.Imported("Radarr", "Movies", 3);
            session.Skip(new ImportSkip("Radarr", "Movies", 4, "x", SkipReason.Corrupt, "d"));
            session.Skip(new ImportSkip("Radarr", "Movies", 5, "y", SkipReason.Corrupt, "d"));

            var rows = ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Movies")) });

            rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new { App = "Radarr", Table = "Movies", Read = 5L, Imported = 3L, Skipped = 2L, Balanced = true });
        }

        [Test]
        public void should_not_balance_when_a_row_is_lost()
        {
            var session = Session(Snapshot("Radarr", "Movies", 5));
            session.Imported("Radarr", "Movies", 4);

            ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Movies")) })
                .Should().ContainSingle().Which.Balanced.Should().BeFalse();
        }

        [Test]
        public void should_not_balance_when_a_row_is_counted_twice()
        {
            var session = Session(Snapshot("Radarr", "Movies", 2));
            session.Imported("Radarr", "Movies", 2);
            session.Skip(new ImportSkip("Radarr", "Movies", 2, "x", SkipReason.Corrupt, "d"));

            ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Movies")) })
                .Should().ContainSingle().Which.Balanced.Should().BeFalse();
        }

        [Test]
        public void should_only_check_tables_a_stage_claims()
        {
            var session = Session(Snapshot("Radarr", "Movies", 5), Snapshot("Radarr", "Credits", 9));

            ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Movies")) })
                .Should().ContainSingle(r => r.Table == "Movies");
        }

        [Test]
        public void should_keep_two_apps_tables_of_the_same_name_apart()
        {
            var session = Session(Snapshot("Radarr", "Tags", 1), Snapshot("Prowlarr", "Tags", 2));
            session.Imported("Radarr", "Tags", 1);
            session.Imported("Prowlarr", "Tags", 2);

            var rows = ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Tags"), new SourceTableRef("Prowlarr", "Tags")) });

            rows.Should().HaveCount(2).And.OnlyContain(r => r.Balanced);
        }

        [Test]
        public void should_not_count_a_skip_from_another_apps_table()
        {
            var session = Session(Snapshot("Radarr", "Tags", 1), Snapshot("Prowlarr", "Tags", 1));
            session.Imported("Radarr", "Tags", 1);
            session.Skip(new ImportSkip("Prowlarr", "Tags", 1, null, SkipReason.Corrupt, "d"));

            var rows = ImportReconciliation.Check(session, new[] { new Claims(new SourceTableRef("Radarr", "Tags"), new SourceTableRef("Prowlarr", "Tags")) });

            rows.Should().OnlyContain(r => r.Balanced);
            rows.Single(r => r.App == "Radarr").Skipped.Should().Be(0);
        }

        [Test]
        public void verification_failure_should_name_tables_and_counts_only()
        {
            var failure = new ImportVerificationException(new[] { new ReconciliationRow("Radarr", "Movies", 5, 4, 0) });

            failure.Message.Should().Contain("Radarr Movies (read 5, imported 4, skipped 0)");
        }

        [Test]
        public void imported_rows_should_accumulate_per_table_without_regard_to_case()
        {
            var session = Session();

            session.Imported("Radarr", "Tags");
            session.Imported("radarr", "TAGS", 2);

            session.ImportedRows("Radarr", "Tags").Should().Be(3);
            session.ImportedRows("Radarr", "Movies").Should().Be(0);
        }

        [Test]
        public void snapshot_lookup_should_ignore_case_and_return_null_for_an_app_not_given()
        {
            var session = Session(Snapshot("Radarr", "Movies", 1));

            session.Snapshot("radarr").Should().NotBeNull();
            session.Snapshot("Prowlarr").Should().BeNull();
            ((Action)(() => _ = session.Snapshots.Count)).Should().NotThrow();
        }
    }
}
