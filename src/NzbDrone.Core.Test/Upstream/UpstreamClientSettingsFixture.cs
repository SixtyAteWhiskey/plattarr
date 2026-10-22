using System;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // Inputs are the shape found in the owner's Radarr DownloadClients.Settings
    // (qBittorrent): movieCategory, recentMoviePriority and olderMoviePriority stored,
    // movieImportedCategory absent because it was never set.
    // ModuleDownloadClientCategoriesFixture's sibling, UpstreamClientSettingsMigrationFixture,
    // proves the result equals what migration 003 does to the same row.
    [TestFixture]
    public class UpstreamClientSettingsFixture : CoreTest
    {
        private const string QBittorrent =
            "{\"host\":\"qbit.invalid\",\"port\":8080,\"useSsl\":false,\"username\":\"user\",\"password\":\"FAKE-PASSWORD\","
            + "\"movieCategory\":\"radarr\",\"recentMoviePriority\":0,\"olderMoviePriority\":0,\"initialState\":0,"
            + "\"sequentialOrder\":false,\"firstAndLast\":false,\"contentLayout\":0}";

        private static JObject Convert(string implementation, string settings)
        {
            return JObject.Parse(DownloadClientSettingsConverter.Convert(implementation, settings));
        }

        [Test]
        public void should_move_the_movie_keys_into_one_movies_entry()
        {
            var result = Convert("QBittorrent", QBittorrent);

            var entry = result["moduleCategories"].Should().BeOfType<JArray>().Which.Should().ContainSingle().Which;

            ((string)entry["kind"]).Should().Be("movies");
            ((string)entry["category"]).Should().Be("radarr");
            ((int)entry["recentPriority"]).Should().Be(0);
            ((int)entry["olderPriority"]).Should().Be(0);
        }

        [Test]
        public void should_keep_the_users_own_category_and_not_use_the_default()
        {
            var result = Convert("QBittorrent", QBittorrent.Replace("\"radarr\"", "\"my-films\""));

            ((string)result["moduleCategories"][0]["category"]).Should().Be("my-films");
        }

        [Test]
        public void should_remove_the_old_keys_and_keep_every_other_setting()
        {
            var result = Convert("QBittorrent", QBittorrent);

            result.Properties().Select(p => p.Name).Should().BeEquivalentTo(
                "host", "port", "useSsl", "username", "password", "initialState", "sequentialOrder", "firstAndLast", "contentLayout", "moduleCategories");

            ((string)result["host"]).Should().Be("qbit.invalid");
            ((int)result["port"]).Should().Be(8080);
            ((string)result["password"]).Should().Be("FAKE-PASSWORD");
        }

        [Test]
        public void should_leave_an_unstored_imported_category_unstored()
        {
            var entry = Convert("QBittorrent", QBittorrent)["moduleCategories"][0];

            ((JObject)entry).ContainsKey("importedCategory").Should().BeFalse();
        }

        [Test]
        public void should_carry_a_stored_imported_category()
        {
            var settings = QBittorrent.Replace("\"movieCategory\"", "\"movieImportedCategory\":\"radarr-done\",\"movieCategory\"");

            ((string)Convert("QBittorrent", settings)["moduleCategories"][0]["importedCategory"]).Should().Be("radarr-done");
        }

        [Test]
        public void should_keep_an_empty_category_empty()
        {
            var entry = Convert("QBittorrent", QBittorrent.Replace("\"radarr\"", "\"\""))["moduleCategories"][0];

            ((string)entry["category"]).Should().BeEmpty();
        }

        [Test]
        public void should_carry_priorities_that_are_not_zero()
        {
            var settings = QBittorrent.Replace("\"recentMoviePriority\":0", "\"recentMoviePriority\":3").Replace("\"olderMoviePriority\":0", "\"olderMoviePriority\":4");

            var entry = Convert("QBittorrent", settings)["moduleCategories"][0];

            ((int)entry["recentPriority"]).Should().Be(3);
            ((int)entry["olderPriority"]).Should().Be(4);
        }

        [Test]
        public void should_read_the_old_keys_in_any_case()
        {
            var settings = QBittorrent.Replace("\"movieCategory\"", "\"MovieCategory\"");

            var result = Convert("QBittorrent", settings);

            ((string)result["moduleCategories"][0]["category"]).Should().Be("radarr");
            result.ContainsKey("MovieCategory").Should().BeFalse();
        }

        [Test]
        public void should_handle_sabnzbd()
        {
            var settings = "{\"host\":\"sab.invalid\",\"port\":8080,\"apiKey\":\"FAKEAPIKEY0000\",\"movieCategory\":\"movies\",\"recentMoviePriority\":-100,\"olderMoviePriority\":-100}";

            var result = Convert("Sabnzbd", settings);

            ((string)result["moduleCategories"][0]["category"]).Should().Be("movies");
            ((int)result["moduleCategories"][0]["recentPriority"]).Should().Be(-100);
            ((string)result["apiKey"]).Should().Be("FAKEAPIKEY0000");
        }

        [Test]
        public void should_keep_a_list_that_is_already_there_and_still_drop_the_old_keys()
        {
            var settings = "{\"host\":\"qbit.invalid\",\"movieCategory\":\"stale\",\"moduleCategories\":[{\"kind\":\"movies\",\"category\":\"kept\"}]}";

            var result = Convert("QBittorrent", settings);

            result["moduleCategories"].Should().HaveCount(1);
            ((string)result["moduleCategories"][0]["category"]).Should().Be("kept");
            result.ContainsKey("movieCategory").Should().BeFalse();
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        [TestCase("[]")]
        [TestCase("\"text\"")]
        public void should_return_settings_that_hold_nothing_to_move_unchanged(string settings)
        {
            DownloadClientSettingsConverter.Convert("QBittorrent", settings).Should().Be(settings);
        }

        [Test]
        public void should_refuse_settings_that_are_not_valid_json()
        {
            Action convert = () => DownloadClientSettingsConverter.Convert("QBittorrent", "{\"host\":");

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [TestCase("QBittorrent", true)]
        [TestCase("qbittorrent", true)]
        [TestCase("Sabnzbd", true)]
        [TestCase("NzbVortex", false)]
        [TestCase("Transmission", false)]
        [TestCase("", false)]
        public void should_handle_only_the_clients_this_build_has(string implementation, bool expected)
        {
            DownloadClientSettingsConverter.Handles(implementation).Should().Be(expected);
        }

        [Test]
        public void should_refuse_to_convert_a_client_it_does_not_handle()
        {
            Action convert = () => DownloadClientSettingsConverter.Convert("Transmission", QBittorrent);

            convert.Should().Throw<ArgumentException>();
        }
    }
}
