using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Languages.Upstream;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The profiles stage against a migrated target, with source rows shaped like the ones a
    // live Radarr 5.x database holds (session 30 dump): quality items as leaves and groups,
    // format scores that point at custom format IDs, NULL size limits, a default delay profile.
    [TestFixture]
    public class ProfilesStageFixture : DbTest
    {
        private const string ReleaseTitleSpecification =
            "{\"type\":\"ReleaseTitleSpecification\",\"body\":{\"order\":1,\"implementationName\":\"Release Title\",\"name\":\"No executables\",\"negate\":false,\"required\":true,\"value\":\"\\\\.(exe|bat)$\"}}";

        private const string SpanishSpecification =
            "{\"type\":\"LanguageSpecification\",\"body\":{\"order\":3,\"implementationName\":\"Language\",\"name\":\"Spanish\",\"negate\":false,\"required\":false,\"value\":3,\"exceptLanguage\":false}}";

        private CustomFormatRepository _customFormats;
        private QualityProfileRepository _profiles;
        private QualityDefinitionRepository _definitions;
        private DelayProfileRepository _delayProfiles;
        private TagRepository _tags;

        [SetUp]
        public void SetUpStages()
        {
            _customFormats = Mocker.Resolve<CustomFormatRepository>();

            // The profile repository turns stored format IDs into formats through this service.
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(() => _customFormats.All().ToList());

            _profiles = Mocker.Resolve<QualityProfileRepository>();
            _definitions = Mocker.Resolve<QualityDefinitionRepository>();
            _delayProfiles = Mocker.Resolve<DelayProfileRepository>();
            _tags = Mocker.Resolve<TagRepository>();

            Mocker.SetConstant<ICustomFormatRepository>(_customFormats);
            Mocker.SetConstant<IQualityProfileRepository>(_profiles);
            Mocker.SetConstant<IQualityDefinitionRepository>(_definitions);
            Mocker.SetConstant<IDelayProfileRepository>(_delayProfiles);
            Mocker.SetConstant<ITagRepository>(_tags);
            Mocker.SetConstant<IEnumerable<IUpstreamCatalog>>(new IUpstreamCatalog[] { new RadarrCatalog(), new SonarrCatalog() });
            Mocker.SetConstant<IEnumerable<IImportStage>>(new IImportStage[] { Mocker.Resolve<TagsStage>(), Mocker.Resolve<ProfilesStage>() });
        }

        private ImportResult Run(params string[] rows)
        {
            var snapshot = UpstreamFixtureTest.ReadSnapshot(
                new RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                new[] { UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'") }.Concat(rows),
                TempFolder);

            return Mocker.Resolve<UpstreamImporter>().Run(new[] { snapshot }, new ImportOptions(), false);
        }

        // Both apps at once: the Radarr rows and the Sonarr rows (Sonarr 4.0.20 schema), each with
        // its own "Kids" tag.
        private ImportResult RunBoth(IEnumerable<string> radarrRows, IEnumerable<string> sonarrRows)
        {
            var tag = new[] { UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'") };

            var radarr = UpstreamFixtureTest.ReadSnapshot(
                new RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                tag.Concat(radarrRows),
                TempFolder);

            var sonarrFolder = System.IO.Path.Combine(TempFolder, "sonarr");
            System.IO.Directory.CreateDirectory(sonarrFolder);

            var sonarr = UpstreamFixtureTest.ReadSnapshot(
                new SonarrAdapter(),
                GetTestPath("Files/Upstream/sonarr-217.schema.sql"),
                217,
                tag.Concat(sonarrRows),
                sonarrFolder);

            return Mocker.Resolve<UpstreamImporter>().Run(new[] { radarr, sonarr }, new ImportOptions(), false);
        }

        // Sonarr 4 profiles have no Language column.
        private static string SonarrProfile(int id, string name, int cutoff, string items, string formatItems = "[]")
        {
            return UpstreamFixtureTest.Insert(
                "QualityProfiles",
                "Id, Name, Cutoff, Items, UpgradeAllowed, FormatItems, MinFormatScore, CutoffFormatScore, MinUpgradeFormatScore",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, {cutoff}, {UpstreamFixtureTest.Sql("[" + items + "]")}, 1, {UpstreamFixtureTest.Sql(formatItems)}, 0, 0, 1");
        }

        private static string IndexerFlagSpecification(int value)
        {
            return $"{{\"type\":\"IndexerFlagSpecification\",\"body\":{{\"order\":1,\"implementationName\":\"Indexer Flag\",\"name\":\"Flag\",\"negate\":false,\"required\":true,\"value\":{value}}}}}";
        }

        private static string CustomFormat(int id, string name, params string[] specifications)
        {
            return UpstreamFixtureTest.Insert(
                "CustomFormats",
                "Id, Name, Specifications, IncludeCustomFormatWhenRenaming",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, {UpstreamFixtureTest.Sql("[" + string.Join(",", specifications) + "]")}, 1");
        }

        private static string Leaf(int quality, bool allowed)
        {
            return $"{{\"quality\":{quality},\"items\":[],\"allowed\":{(allowed ? "true" : "false")}}}";
        }

        private static string Group(int id, string name, bool allowed, params int[] members)
        {
            var items = string.Join(",", members.Select(m => Leaf(m, true)));

            return $"{{\"id\":{id},\"name\":\"{name}\",\"items\":[{items}],\"allowed\":{(allowed ? "true" : "false")}}}";
        }

        private static string Profile(int id, string name, int cutoff, string items, string formatItems = "[]", string language = "1")
        {
            return UpstreamFixtureTest.Insert(
                "QualityProfiles",
                "Id, Name, Cutoff, Items, Language, FormatItems, UpgradeAllowed, MinFormatScore, CutoffFormatScore, MinUpgradeFormatScore",
                $"{id}, {UpstreamFixtureTest.Sql(name)}, {cutoff}, {UpstreamFixtureTest.Sql("[" + items + "]")}, {language}, {UpstreamFixtureTest.Sql(formatItems)}, 1, 5, 100, 2");
        }

        private static string Definition(int id, int quality, string title, string min, string max, string preferred)
        {
            return UpstreamFixtureTest.Insert(
                "QualityDefinitions",
                "Id, Quality, Title, MinSize, MaxSize, PreferredSize",
                $"{id}, {quality}, {UpstreamFixtureTest.Sql(title)}, {min}, {max}, {preferred}");
        }

        private static string Delay(int id, int order, string tags, int torrentDelay = 0)
        {
            return UpstreamFixtureTest.Insert(
                "DelayProfiles",
                "Id, EnableUsenet, EnableTorrent, PreferredProtocol, UsenetDelay, TorrentDelay, \"Order\", Tags, BypassIfHighestQuality, BypassIfAboveCustomFormatScore, MinimumCustomFormatScore",
                $"{id}, 1, 1, 2, 10, {torrentDelay}, {order}, {UpstreamFixtureTest.Sql(tags)}, 1, 1, NULL");
        }

        [Test]
        public void should_write_custom_formats_and_remap_the_language_condition()
        {
            var result = Run(CustomFormat(1, "Spanish only", ReleaseTitleSpecification, SpanishSpecification));

            var format = _customFormats.All().Single();

            format.Name.Should().Be("Spanish only");
            format.IncludeCustomFormatWhenRenaming.Should().BeTrue();
            format.AppliesTo.Should().Equal(MediaKind.Movies);
            format.Specifications.Should().HaveCount(2);
            format.Specifications.OfType<LanguageSpecification>().Single().Value.Should().Be(RadarrLanguageIds.ToCatalog(3)).And.Be(Language.Spanish.Id);
            result.IdMap.Resolve("Radarr", "CustomFormats", 1).Should().Be(format.Id);
        }

        [Test]
        public void should_skip_a_custom_format_with_a_specification_the_target_does_not_have()
        {
            var unknown = "{\"type\":\"NoSuchSpecification\",\"body\":{\"order\":1,\"name\":\"x\",\"negate\":false,\"required\":false}}";

            var result = Run(CustomFormat(1, "Broken", unknown), CustomFormat(2, "Fine", ReleaseTitleSpecification));

            result.Skips.Should().ContainSingle(s => s.Table == "CustomFormats" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
            _customFormats.All().Select(f => f.Name).Should().Equal("Fine");
            result.Tables.Single(t => t.Table == "CustomFormats").Should().Match<ReconciliationRow>(r => r.Read == 2 && r.Imported == 1 && r.Skipped == 1);
        }

        [Test]
        public void should_skip_a_custom_format_with_a_language_the_catalog_does_not_have()
        {
            var unknown = SpanishSpecification.Replace("\"value\":3", "\"value\":9999");

            var result = Run(CustomFormat(1, "Broken", unknown));

            result.Skips.Should().ContainSingle(s => s.Table == "CustomFormats" && s.Reason == SkipReason.Corrupt);
            _customFormats.All().Should().BeEmpty();
        }

        [Test]
        public void should_remap_the_qualities_of_leaves_and_groups_and_keep_group_ids()
        {
            var items = string.Join(",", Leaf(0, false), Leaf(7, true), Group(1000, "WEB 1080p", true, 3, 15));

            Run(Profile(1, "HD", 7, items));

            var profile = _profiles.All().Single();

            profile.Name.Should().Be("HD");
            profile.Items.Should().HaveCount(3);
            profile.Items[1].Quality.Id.Should().Be(Quality.Bluray1080p.Id).And.NotBe(7);
            profile.Items[1].Allowed.Should().BeTrue();
            profile.Items[2].Id.Should().Be(1000);
            profile.Items[2].Name.Should().Be("WEB 1080p");
            profile.Items[2].Quality.Should().BeNull();
            profile.Items[2].Items.Select(i => i.Quality.Id).Should().Equal(Quality.WEBDL1080p.Id, Quality.WEBRip1080p.Id);
            profile.Cutoff.Should().Be(Quality.Bluray1080p.Id);
            profile.UpgradeAllowed.Should().BeTrue();
            profile.MinFormatScore.Should().Be(5);
            profile.CutoffFormatScore.Should().Be(100);
            profile.MinUpgradeFormatScore.Should().Be(2);
            profile.AppliesTo.Should().Equal(MediaKind.Movies);
        }

        [Test]
        public void should_keep_a_group_cutoff_and_remap_a_language()
        {
            var items = string.Join(",", Leaf(7, true), Group(1000, "WEB 1080p", true, 3, 15));

            Run(Profile(1, "Group cutoff", 1000, items, language: "3"));

            var profile = _profiles.All().Single();

            profile.Cutoff.Should().Be(1000);
            profile.Language.Id.Should().Be(Language.Spanish.Id);
        }

        [Test]
        public void should_skip_a_profile_whose_cutoff_names_a_group_it_does_not_have()
        {
            var result = Run(Profile(1, "Bad cutoff", 1005, Leaf(7, true)));

            result.Skips.Should().ContainSingle(s => s.Table == "QualityProfiles" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
            _profiles.All().Should().BeEmpty();
        }

        [Test]
        public void should_skip_a_profile_whose_items_are_not_valid_json_and_import_the_next()
        {
            var result = Run(Profile(1, "Broken", 7, "{not json"), Profile(2, "Fine", 7, Leaf(7, true)));

            result.Skips.Should().ContainSingle(s => s.Table == "QualityProfiles" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
            _profiles.All().Select(p => p.Name).Should().Equal("Fine");
        }

        [Test]
        public void should_skip_a_profile_with_a_quality_radarr_never_had()
        {
            var result = Run(Profile(1, "Bad quality", 7, Leaf(7, true) + "," + Leaf(11, true)));

            result.Skips.Should().ContainSingle(s => s.Table == "QualityProfiles" && s.Reason == SkipReason.Corrupt);
            result.Tables.Single(t => t.Table == "QualityProfiles").Balanced.Should().BeTrue();
        }

        [Test]
        public void should_point_format_scores_at_the_imported_custom_formats()
        {
            // Two formats, so the target IDs can differ from the source IDs: format 5 is
            // written first and gets target ID 1.
            var scores = "[{\"format\":5,\"score\":-10000},{\"format\":9,\"score\":250}]";

            var result = Run(
                CustomFormat(5, "Five", ReleaseTitleSpecification),
                CustomFormat(9, "Nine", ReleaseTitleSpecification),
                Profile(1, "Scored", 7, Leaf(7, true), scores));

            var profile = _profiles.All().Single();
            var five = result.IdMap.Resolve("Radarr", "CustomFormats", 5);
            var nine = result.IdMap.Resolve("Radarr", "CustomFormats", 9);

            profile.FormatItems.Select(f => (f.Format.Id, f.Score)).Should().Equal(((int)five, -10000), ((int)nine, 250));
        }

        [Test]
        public void should_drop_a_score_for_a_custom_format_that_was_not_imported_and_warn()
        {
            var scores = "[{\"format\":5,\"score\":10},{\"format\":77,\"score\":20}]";

            var result = Run(CustomFormat(5, "Five", ReleaseTitleSpecification), Profile(1, "Scored", 7, Leaf(7, true), scores));

            _profiles.All().Single().FormatItems.Should().ContainSingle().Which.Score.Should().Be(10);
            result.Warnings.Should().ContainSingle(w => w.Contains("Scored") && w.Contains("dropped 1"));
        }

        [Test]
        public void should_import_quality_definitions_with_missing_size_limits()
        {
            Run(
                Definition(1, 7, "Bluray-1080p", "0", "NULL", "NULL"),
                Definition(2, 3, "WEBDL-1080p", "3.5", "100", "95.5"));

            var definitions = _definitions.All().ToDictionary(d => d.Quality.Id);

            definitions.Should().HaveCount(2);

            var bluray = definitions[Quality.Bluray1080p.Id];

            bluray.Title.Should().Be("Bluray-1080p");
            bluray.MinSize.Should().Be(0);
            bluray.MaxSize.Should().BeNull();
            bluray.PreferredSize.Should().BeNull();

            var web = definitions[Quality.WEBDL1080p.Id];

            web.MinSize.Should().Be(3.5);
            web.MaxSize.Should().Be(100);
            web.PreferredSize.Should().Be(95.5);
        }

        [Test]
        public void should_skip_a_quality_definition_for_a_quality_radarr_never_had()
        {
            var result = Run(Definition(1, 7, "Bluray-1080p", "0", "NULL", "NULL"), Definition(2, 13, "Mystery", "0", "1", "1"));

            result.Skips.Should().ContainSingle(s => s.Table == "QualityDefinitions" && s.SourceId == 2);
            _definitions.All().Should().ContainSingle();
        }

        [Test]
        public void should_update_the_default_delay_profile_in_place_and_insert_the_others()
        {
            var targetDefault = _delayProfiles.All().Single(p => p.Order == int.MaxValue);

            var result = Run(Delay(1, int.MaxValue, "[]", 30), Delay(2, 1, "[\n  1\n]", 60));

            var all = _delayProfiles.All().OrderBy(p => p.Order).ToList();

            all.Should().HaveCount(2);

            var inserted = all[0];
            var updated = all[1];

            updated.Id.Should().Be(targetDefault.Id);
            updated.TorrentDelay.Should().Be(30);
            updated.UsenetDelay.Should().Be(10);
            updated.BypassIfHighestQuality.Should().BeTrue();
            updated.MinimumCustomFormatScore.Should().Be(0);
            inserted.TorrentDelay.Should().Be(60);
            inserted.Tags.Should().Equal((int)result.IdMap.Resolve("Radarr", "Tags", 1));
            result.IdMap.Resolve("Radarr", "DelayProfiles", 1).Should().Be(targetDefault.Id);
            result.IdMap.Resolve("Radarr", "DelayProfiles", 2).Should().Be(inserted.Id);
        }

        [Test]
        public void should_leave_the_target_default_delay_profile_alone_when_the_source_has_none()
        {
            var before = _delayProfiles.All().Single();

            Run(Delay(2, 1, "[]", 60));

            _delayProfiles.All().Should().Contain(p => p.Id == before.Id && p.TorrentDelay == before.TorrentDelay);
        }

        [Test]
        public void should_drop_a_delay_profile_tag_that_was_not_imported_and_warn()
        {
            var result = Run(Delay(2, 1, "[1, 42]", 60));

            _delayProfiles.All().Single(p => p.Order == 1).Tags.Should().HaveCount(1);
            result.Warnings.Should().ContainSingle(w => w.Contains("Delay profile") && w.Contains("dropped 1"));
        }

        [Test]
        public void should_account_for_every_row_of_the_four_tables()
        {
            var result = Run(
                CustomFormat(1, "One", ReleaseTitleSpecification),
                Profile(1, "HD", 7, Leaf(7, true)),
                Definition(1, 7, "Bluray-1080p", "0", "NULL", "NULL"),
                Delay(1, int.MaxValue, "[]"));

            result.Tables.Where(t => t.App == "Radarr" && t.Table != "Tags")
                .Select(t => (t.Table, t.Read, t.Imported, t.Skipped))
                .Should().BeEquivalentTo(new[]
                {
                    ("CustomFormats", 1L, 1L, 0L),
                    ("DelayProfiles", 1L, 1L, 0L),
                    ("QualityDefinitions", 1L, 1L, 0L),
                    ("QualityProfiles", 1L, 1L, 0L)
                });
        }

        [Test]
        public void should_merge_an_identical_custom_format_from_both_apps_into_one_for_movies_and_tv()
        {
            var result = RunBoth(
                new[] { CustomFormat(1, "No executables", ReleaseTitleSpecification) },
                new[] { CustomFormat(4, "No executables", ReleaseTitleSpecification) });

            var format = _customFormats.All().Single();

            format.AppliesTo.Should().BeEquivalentTo(new[] { MediaKind.Movies, MediaKind.Tv });
            result.IdMap.Resolve("Radarr", "CustomFormats", 1).Should().Be(format.Id);
            result.IdMap.Resolve("Sonarr", "CustomFormats", 4).Should().Be(format.Id);
            result.Warnings.Should().NotContain(w => w.Contains("exists in more than one app"));
        }

        [Test]
        public void should_rename_the_sonarr_custom_format_when_the_same_name_has_different_settings()
        {
            var result = RunBoth(
                new[] { CustomFormat(1, "Spanish", SpanishSpecification) },
                new[] { CustomFormat(1, "Spanish", ReleaseTitleSpecification) });

            var formats = _customFormats.All().ToDictionary(f => f.Name);

            formats.Keys.Should().BeEquivalentTo("Spanish", "Spanish (Sonarr)");
            formats["Spanish"].AppliesTo.Should().Equal(MediaKind.Movies);
            formats["Spanish (Sonarr)"].AppliesTo.Should().Equal(MediaKind.Tv);
            result.Warnings.Should().ContainSingle(w => w.StartsWith("Custom format \"Spanish\" exists in more than one app") && w.Contains("\"Spanish (Sonarr)\""));
        }

        [Test]
        public void should_translate_a_sonarr_indexer_flag_condition_to_the_global_flag()
        {
            // Sonarr's bit 8 is Internal; the global flag has a different value.
            RunBoth(Array.Empty<string>(), new[] { CustomFormat(1, "Internal", IndexerFlagSpecification(8)) });

            var specification = _customFormats.All().Single().Specifications.OfType<NzbDrone.Core.CustomFormats.IndexerFlagSpecification>().Single();

            specification.Value.Should().Be((int)NzbDrone.Core.Parser.Model.IndexerFlags.G_Internal);
        }

        [Test]
        public void should_skip_a_sonarr_custom_format_with_an_indexer_flag_sonarr_never_had()
        {
            var result = RunBoth(Array.Empty<string>(), new[] { CustomFormat(1, "Mystery flag", IndexerFlagSpecification(256)) });

            result.Skips.Should().ContainSingle(s => s.App == "Sonarr" && s.Table == "CustomFormats" && s.SourceId == 1 && s.Reason == SkipReason.Corrupt);
            _customFormats.All().Should().BeEmpty();
        }

        [Test]
        public void should_rename_a_sonarr_quality_profile_whose_name_radarr_already_uses()
        {
            // Sonarr quality 4 is HDTV-720p.
            var result = RunBoth(
                new[] { Profile(1, "HD-720p", 4, Leaf(4, true)) },
                new[] { SonarrProfile(3, "HD-720p", 4, Leaf(4, true)) });

            var profiles = _profiles.All().ToDictionary(p => p.Name);

            profiles.Keys.Should().BeEquivalentTo("HD-720p", "HD-720p (Sonarr)");
            profiles["HD-720p (Sonarr)"].AppliesTo.Should().Equal(MediaKind.Tv);
            profiles["HD-720p (Sonarr)"].Language.Should().Be(Language.Unknown);
            profiles["HD-720p (Sonarr)"].Cutoff.Should().Be(Quality.HDTV720p.Id);
            result.IdMap.Resolve("Sonarr", "QualityProfiles", 3).Should().Be(profiles["HD-720p (Sonarr)"].Id);
            result.Warnings.Should().ContainSingle(w => w.StartsWith("Quality profile \"HD-720p\" exists in more than one app"));
        }

        [Test]
        public void should_add_the_qualities_a_sonarr_profile_does_not_list_as_not_allowed_at_the_bottom()
        {
            RunBoth(Array.Empty<string>(), new[] { SonarrProfile(1, "TV", 4, Leaf(4, true)) });

            var profile = _profiles.All().Single();

            profile.Items.Should().HaveCount(Quality.DefaultQualityDefinitions.Count);
            profile.Items.Last().Quality.Should().Be(Quality.HDTV720p);
            profile.Items.Where(i => i.Allowed).Should().ContainSingle();
            profile.Items.Select(i => i.Quality.Id).Should().OnlyHaveUniqueItems();
        }

        [Test]
        public void should_keep_one_quality_definition_per_quality_and_warn_when_the_sonarr_sizes_differ()
        {
            var result = RunBoth(
                new[] { Definition(1, 7, "Bluray-1080p", "2", "100", "95"), Definition(2, 3, "WEBDL-1080p", "2", "100", "95") },
                new[] { Definition(7, 7, "Bluray-1080p", "4", "130", "100"), Definition(3, 3, "WEBDL-1080p", "2", "100", "95") });

            var definitions = _definitions.All().ToList();

            definitions.Should().HaveCount(2);
            definitions.Single(d => d.Quality.Id == Quality.Bluray1080p.Id).MaxSize.Should().Be(100);
            result.IdMap.Resolve("Sonarr", "QualityDefinitions", 7).Should().Be(result.IdMap.Resolve("Radarr", "QualityDefinitions", 1));
            result.Warnings.Should().ContainSingle(w => w.StartsWith("Size limits are one setting per quality") && w.Contains("for 1 quality definitions") && w.Contains("Bluray-1080p"));
        }

        [Test]
        public void should_keep_the_radarr_default_delay_profile_and_report_a_different_sonarr_one()
        {
            var result = RunBoth(
                new[] { Delay(1, int.MaxValue, "[]", 30) },
                new[] { Delay(1, int.MaxValue, "[]", 120) });

            _delayProfiles.All().Single(p => p.Order == int.MaxValue).TorrentDelay.Should().Be(30);
            result.Skips.Should().ContainSingle(s => s.App == "Sonarr" && s.Table == "DelayProfiles" && s.SourceId == 1 && s.Reason == SkipReason.ConfigConflict);
        }

        [Test]
        public void should_merge_a_sonarr_default_delay_profile_that_matches_the_radarr_one()
        {
            var result = RunBoth(
                new[] { Delay(1, int.MaxValue, "[]", 30) },
                new[] { Delay(1, int.MaxValue, "[]", 30) });

            _delayProfiles.All().Should().ContainSingle();
            result.Skips.Should().BeEmpty();
            result.IdMap.Resolve("Sonarr", "DelayProfiles", 1).Should().Be(result.IdMap.Resolve("Radarr", "DelayProfiles", 1));
        }

        [Test]
        public void should_do_nothing_without_a_radarr_database()
        {
            Mocker.Resolve<UpstreamImporter>().Run(Array.Empty<SourceSnapshot>(), new ImportOptions(), false);

            _customFormats.All().Should().BeEmpty();
            _delayProfiles.All().Should().ContainSingle(p => p.Order == int.MaxValue);
        }
    }
}
