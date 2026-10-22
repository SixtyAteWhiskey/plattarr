using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class TagMergerFixture : UpstreamFixtureTest
    {
        [Test]
        public void should_merge_tags_with_the_same_label_ignoring_case()
        {
            // Radarr has "Kids"; Prowlarr has "flaresolverr" and "kids".
            var plan = TagMerger.Plan(new[] { ReadRadarr(), ReadProwlarr() });

            plan.Tags.Select(t => t.Label).Should().Equal("Kids", "flaresolverr");
            plan.Tags[0].Sources.Should().Equal(("Radarr", 1L), ("Prowlarr", 2L));
            plan.Tags[1].Sources.Should().Equal(("Prowlarr", 1L));
            plan.Skips.Should().BeEmpty();
        }

        [Test]
        public void should_keep_the_label_as_first_seen()
        {
            TagMerger.Plan(new[] { ReadProwlarr(), ReadRadarr() }).Tags.Select(t => t.Label)
                .Should().Equal("flaresolverr", "kids");
        }

        [Test]
        public void should_point_every_merged_source_at_the_one_target()
        {
            var plan = TagMerger.Plan(new[] { ReadRadarr(), ReadProwlarr() });
            var map = new IdMap();

            plan.Tags[0].RecordTarget(map, 7);
            plan.Tags[1].RecordTarget(map, 8);

            map.Resolve("Radarr", "Tags", 1).Should().Be(7);
            map.Resolve("Prowlarr", "Tags", 2).Should().Be(7);
            map.Resolve("Prowlarr", "Tags", 1).Should().Be(8);
        }

        [Test]
        public void should_ignore_spaces_around_a_label()
        {
            var prowlarr = ReadProwlarr(Insert("Tags", "Id, Label", "3, '  Kids '"));

            var plan = TagMerger.Plan(new[] { ReadRadarr(), prowlarr });

            plan.Tags.Select(t => t.Label).Should().Equal("Kids", "flaresolverr");
            plan.Tags[0].Sources.Should().Contain(("Prowlarr", 3L));
        }

        [Test]
        public void should_skip_a_tag_with_no_label_as_corrupt()
        {
            var prowlarr = ReadProwlarr(Insert("Tags", "Id, Label", "3, '   '"));

            var plan = TagMerger.Plan(new[] { prowlarr });

            plan.Tags.Select(t => t.Label).Should().Equal("flaresolverr", "kids");
            plan.Skips.Should().ContainSingle();
            plan.Skips[0].App.Should().Be("Prowlarr");
            plan.Skips[0].Table.Should().Be("Tags");
            plan.Skips[0].SourceId.Should().Be(3);
            plan.Skips[0].Reason.Should().Be(SkipReason.Corrupt);
        }

        [Test]
        public void should_account_for_every_source_tag()
        {
            var plan = TagMerger.Plan(new[] { ReadRadarr(), ReadProwlarr() });

            // 1 Radarr tag + 2 Prowlarr tags, merged into 2 target tags.
            (plan.Tags.Sum(t => t.Sources.Count) + plan.Skips.Count).Should().Be(3);
        }
    }

    [TestFixture]
    public class TagRemapperFixture : UpstreamFixtureTest
    {
        private static IdMap MapWith(params (string App, long Source, long Target)[] entries)
        {
            var map = new IdMap();

            foreach (var (app, source, target) in entries)
            {
                map.Record(app, "Tags", source, target);
            }

            return map;
        }

        [Test]
        public void should_translate_tag_ids()
        {
            var map = MapWith(("Radarr", 1, 10), ("Radarr", 2, 20));

            var result = TagRemapper.Remap("Radarr", JToken.Parse("[2,1]"), map, out var dropped);

            result.Should().Equal(20, 10);
            dropped.Should().Be(0);
        }

        [Test]
        public void should_drop_and_count_a_tag_that_no_longer_exists()
        {
            var map = MapWith(("Radarr", 1, 10));

            var result = TagRemapper.Remap("Radarr", JToken.Parse("[1,5,6]"), map, out var dropped);

            result.Should().Equal(10);
            dropped.Should().Be(2);
        }

        [Test]
        public void should_use_the_map_of_the_source_app()
        {
            var map = MapWith(("Radarr", 1, 10), ("Prowlarr", 1, 20));

            TagRemapper.Remap("Prowlarr", JToken.Parse("[1]"), map, out _).Should().Equal(20);
        }

        [Test]
        public void should_collapse_tags_that_merged_into_one()
        {
            var map = MapWith(("Radarr", 1, 10), ("Radarr", 2, 10));

            TagRemapper.Remap("Radarr", JToken.Parse("[1,2]"), map, out _).Should().Equal(10);
        }

        [TestCase(null)]
        [TestCase("null")]
        [TestCase("[]")]
        public void should_treat_an_empty_column_as_no_tags(string json)
        {
            var token = json == null ? null : JToken.Parse(json);

            TagRemapper.Remap("Radarr", token, new IdMap(), out var dropped).Should().BeEmpty();
            dropped.Should().Be(0);
        }

        [TestCase("{}")]
        [TestCase("\"1\"")]
        [TestCase("5")]
        public void should_refuse_a_column_that_is_not_an_array(string json)
        {
            Action remap = () => TagRemapper.Remap("Radarr", JToken.Parse(json), new IdMap(), out _);

            remap.Should().Throw<CorruptSourceDataException>();
        }

        [TestCase("[\"1\"]")]
        [TestCase("[1.5]")]
        [TestCase("[null]")]
        public void should_refuse_an_element_that_is_not_an_integer(string json)
        {
            Action remap = () => TagRemapper.Remap("Radarr", JToken.Parse(json), new IdMap(), out _);

            remap.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_read_the_tags_column_of_a_real_row()
        {
            var radarr = ReadRadarr();
            var plan = TagMerger.Plan(new[] { radarr });
            var map = new IdMap();

            plan.Tags[0].RecordTarget(map, 4);

            var client = radarr.Rows("DownloadClients").Single();

            TagRemapper.Remap("Radarr", client.GetJson("Tags"), map, out var dropped).Should().Equal(4);
            dropped.Should().Be(0);
        }
    }
}
