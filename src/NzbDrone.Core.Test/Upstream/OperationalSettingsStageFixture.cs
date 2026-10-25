using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Profiles.Releases;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;
using Importer = NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class OperationalSettingsStageFixture : DbTest
    {
        private IConfigRepository _configs;
        private IReleaseProfileRepository _profiles;

        [SetUp]
        public void SetUpStages()
        {
            _configs = Mocker.Resolve<ConfigRepository>();
            _profiles = Mocker.Resolve<ReleaseProfileRepository>();

            Mocker.SetConstant<IConfigRepository>(_configs);
            Mocker.SetConstant<IReleaseProfileRepository>(_profiles);

            var tags = Mocker.Resolve<TagRepository>();
            Mocker.SetConstant<ITagRepository>(tags);
            Mocker.SetConstant<IEnumerable<Importer.IImportStage>>(new Importer.IImportStage[]
            {
                Mocker.Resolve<Importer.TagsStage>(),
                Mocker.Resolve<Importer.ReleaseProfilesStage>(),
                Mocker.Resolve<Importer.ConfigStage>()
            });
        }

        private Importer.SourceSnapshot RadarrSnapshot()
        {
            var rows = new List<string>
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "1, 'Kids'")
            };
            rows.AddRange(ConfigRows(new[]
            {
                ("maximumsize", "15000"),
                ("autounmonitorpreviouslydownloadedmovies", "True"),
                ("cleanupmetadataimages", "False"),
                ("hmacpassphrase", "DO-NOT-COPY-RADARR-HMAC"),
                ("hmacsalt", "DO-NOT-COPY-RADARR-HMAC-SALT"),
                ("rijndaelpassphrase", "DO-NOT-COPY-RADARR-CIPHER"),
                ("rijndaelsalt", "DO-NOT-COPY-RADARR-CIPHER-SALT"),
                ("plexclientidentifier", "DO-NOT-COPY-RADARR-DEVICE")
            }));

            return UpstreamFixtureTest.ReadSnapshot(
                new Importer.RadarrAdapter(),
                GetTestPath("Files/Upstream/radarr-242.schema.sql"),
                242,
                rows,
                TempFolder);
        }

        private Importer.SourceSnapshot SonarrSnapshot(string maximumSize = "15000")
        {
            var rows = new List<string>
            {
                UpstreamFixtureTest.Insert("Tags", "Id, Label", "3, 'Kids'"),
                UpstreamFixtureTest.Insert(
                    "ReleaseProfiles",
                    "Id, Required, Ignored, Tags, Enabled, IndexerId, Name",
                    "1, '[]', '[\"bad-group\"]', '[3]', 1, 0, 'Anime releases'")
            };
            rows.AddRange(ConfigRows(new[]
            {
                ("maximumsize", maximumSize),
                ("autounmonitorpreviouslydownloadedepisodes", "True"),
                ("cleanupmetadataimages", "False"),
                ("enablecompleteddownloadhandling", "True"),
                ("hmacpassphrase", "DO-NOT-COPY-SONARR-HMAC"),
                ("hmacsalt", "DO-NOT-COPY-SONARR-HMAC-SALT"),
                ("rijndaelpassphrase", "DO-NOT-COPY-SONARR-CIPHER"),
                ("rijndaelsalt", "DO-NOT-COPY-SONARR-CIPHER-SALT"),
                ("plexclientidentifier", "DO-NOT-COPY-SONARR-DEVICE")
            }));

            return UpstreamFixtureTest.ReadSnapshot(
                new Importer.SonarrAdapter(),
                GetTestPath("Files/Upstream/sonarr-217.schema.sql"),
                217,
                rows,
                TempFolder);
        }

        private static IEnumerable<string> ConfigRows(IEnumerable<(string Key, string Value)> entries)
        {
            var id = 0;

            foreach (var (key, value) in entries)
            {
                yield return UpstreamFixtureTest.Insert("Config", "Id, Key, Value", $"{++id}, {UpstreamFixtureTest.Sql(key)}, {UpstreamFixtureTest.Sql(value)}");
            }
        }

        [Test]
        public void should_import_active_release_rules_and_only_portable_config_values()
        {
            var result = Mocker.Resolve<Importer.UpstreamImporter>().Run(
                new[] { RadarrSnapshot(), SonarrSnapshot() },
                new Importer.ImportOptions(),
                false);

            result.Outcome.Should().Be(Importer.ImportOutcome.Committed);

            var profile = _profiles.All().Should().ContainSingle().Which;
            profile.Name.Should().Be("Anime releases");
            profile.Enabled.Should().BeTrue();
            profile.Required.Should().BeEmpty();
            profile.Ignored.Should().BeEquivalentTo(new[] { "bad-group" });
            profile.Tags.Should().ContainSingle().Which.Should().Be(checked((int)result.IdMap.Resolve("Sonarr", "Tags", 3)));

            var values = _configs.All().ToDictionary(c => c.Key, c => c.Value);
            values.Should().ContainKey("maximumsize").WhoseValue.Should().Be("15000");
            values.Should().ContainKey("autounmonitorpreviouslydownloadedmovies").WhoseValue.Should().Be("true");
            values.Should().ContainKey("autounmonitorpreviouslydownloadedepisodes").WhoseValue.Should().Be("true");
            values.Should().ContainKey("cleanupmetadataimages").WhoseValue.Should().Be("false");
            values.Should().ContainKey("enablecompleteddownloadhandling").WhoseValue.Should().Be("true");
            values.Keys.Should().NotContain(k => k.Contains("hmac") || k.Contains("rijndael") || k == "plexclientidentifier");

            result.Tables.Should().Contain(t => t.App == "Radarr" && t.Table == "Config" && t.Read == 8 && t.Imported == 3 && t.Skipped == 5);
            result.Tables.Should().Contain(t => t.App == "Sonarr" && t.Table == "Config" && t.Read == 9 && t.Imported == 4 && t.Skipped == 5);
            result.Skips.Should().HaveCount(10).And.OnlyContain(s => s.Table == "Config" && s.Reason == Importer.SkipReason.Instance);
            result.Tables.Should().Contain(t => t.App == "Sonarr" && t.Table == "ReleaseProfiles" && t.Read == 1 && t.Imported == 1 && t.Skipped == 0);
        }

        [Test]
        public void should_keep_radarr_value_when_a_shared_config_key_conflicts()
        {
            var result = Mocker.Resolve<Importer.UpstreamImporter>().Run(
                new[] { RadarrSnapshot(), SonarrSnapshot("12000") },
                new Importer.ImportOptions(),
                false);

            _configs.All().Single(c => c.Key == "maximumsize").Value.Should().Be("15000");
            result.Skips.Should().ContainSingle(s => s.App == "Sonarr" && s.Table == "Config" && s.Name == "maximumsize" && s.Reason == Importer.SkipReason.ConfigConflict);
            result.Warnings.Should().ContainSingle(w => w.Contains("maximumsize") && w.Contains("Radarr's value was kept"));
        }
    }
}
