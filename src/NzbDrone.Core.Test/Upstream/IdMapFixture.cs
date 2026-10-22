using System;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class IdMapFixture : CoreTest
    {
        [Test]
        public void should_resolve_a_recorded_row()
        {
            var map = new IdMap();

            map.Record("Radarr", "Movies", 12, 3);

            map.Resolve("Radarr", "Movies", 12).Should().Be(3);
            map.TryResolve("Radarr", "Movies", 12, out var target).Should().BeTrue();
            target.Should().Be(3);
        }

        [Test]
        public void should_keep_apps_and_tables_apart()
        {
            var map = new IdMap();

            map.Record("Radarr", "Tags", 1, 10);
            map.Record("Prowlarr", "Tags", 1, 20);
            map.Record("Radarr", "Indexers", 1, 30);

            map.Resolve("Radarr", "Tags", 1).Should().Be(10);
            map.Resolve("Prowlarr", "Tags", 1).Should().Be(20);
            map.Resolve("Radarr", "Indexers", 1).Should().Be(30);
        }

        [Test]
        public void should_ignore_case_in_app_and_table_names()
        {
            var map = new IdMap();

            map.Record("Radarr", "Tags", 1, 10);

            map.Resolve("RADARR", "tags", 1).Should().Be(10);
        }

        [Test]
        public void should_let_several_source_rows_share_a_target()
        {
            var map = new IdMap();

            map.Record("Radarr", "Tags", 1, 10);
            map.Record("Prowlarr", "Tags", 2, 10);

            map.Resolve("Radarr", "Tags", 1).Should().Be(10);
            map.Resolve("Prowlarr", "Tags", 2).Should().Be(10);
        }

        [Test]
        public void should_accept_the_same_mapping_twice()
        {
            var map = new IdMap();

            map.Record("Radarr", "Movies", 1, 5);
            map.Record("Radarr", "Movies", 1, 5);

            map.Count.Should().Be(1);
        }

        [Test]
        public void should_refuse_a_second_target_for_one_source_row()
        {
            var map = new IdMap();

            map.Record("Radarr", "Movies", 1, 5);

            Action record = () => map.Record("Radarr", "Movies", 1, 6);

            record.Should().Throw<InvalidOperationException>();
            map.Resolve("Radarr", "Movies", 1).Should().Be(5);
        }

        [Test]
        public void should_report_a_missing_row_without_throwing_from_try_resolve()
        {
            var map = new IdMap();

            map.TryResolve("Radarr", "Movies", 99, out var target).Should().BeFalse();
            target.Should().Be(0);
        }

        [Test]
        public void should_throw_naming_the_row_when_a_required_reference_is_missing()
        {
            var map = new IdMap();

            Action resolve = () => map.Resolve("Radarr", "Movies", 99);

            resolve.Should().Throw<UpstreamImportException>().WithMessage("*Radarr Movies ID 99*");
        }

        [Test]
        public void should_write_the_csv_sorted_by_app_then_table_then_source_id()
        {
            var map = new IdMap();

            map.Record("Radarr", "Movies", 10, 2);
            map.Record("Prowlarr", "Tags", 1, 1);
            map.Record("Radarr", "Movies", 2, 1);
            map.Record("Radarr", "Indexers", 7, 4);

            map.ToCsv().Should().Be(
                "app,table,source_id,target_id\n" +
                "Prowlarr,Tags,1,1\n" +
                "Radarr,Indexers,7,4\n" +
                "Radarr,Movies,2,1\n" +
                "Radarr,Movies,10,2\n");
        }
    }
}
