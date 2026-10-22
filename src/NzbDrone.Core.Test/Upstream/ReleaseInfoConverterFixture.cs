using System;
using System.Data.SQLite;
using System.Linq;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Movies.Upstream;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    // The base row is a grab event's Release as found in the owner's database: 141 of 141
    // grabbed rows have this shape (docs/importer.md, "Dump findings"). The language and
    // indexerFlags variants are not from real rows: every real row has an empty language
    // list and no indexerFlags key. They follow what Radarr's code can write, and are the
    // first thing to compare against a real row when one turns up.
    [TestFixture]
    public class ReleaseInfoConverterFixture : UpstreamFixtureTest
    {
        private readonly RadarrCatalog _catalog = new RadarrCatalog();

        // Radarr indexer 1 was imported as indexer 7; any other ID has no match.
        private static int MapIndexer(int radarrId)
        {
            return radarrId == 1 ? 7 : 0;
        }

        private string RealRelease()
        {
            return ReadRadarr().Rows("DownloadHistory").Single().GetString("Release");
        }

        private static string Changed(Action<JObject> change)
        {
            var release = JObject.Parse(GrabbedRelease);

            change(release);

            return release.ToString();
        }

        private ReleaseInfo Convert(string json)
        {
            return ReleaseInfoConverter.Convert(json, _catalog, MapIndexer);
        }

        [Test]
        public void should_convert_a_real_grab_row()
        {
            var release = Convert(RealRelease());

            release.Title.Should().Be("Sintel.2010.1080p.WEB-DL.x264-BLENDER");
            release.Guid.Should().Be("https://tracker.invalid/details/1001");
            release.InfoUrl.Should().Be("https://tracker.invalid/details/1001");
            release.CommentUrl.Should().Be("https://tracker.invalid/details/1001#comments");
            release.Size.Should().Be(3221225472);
            release.Indexer.Should().Be("Tracker One (Prowlarr)");
            release.IndexerPriority.Should().Be(25);
            release.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
            release.TmdbId.Should().Be(45745);
            release.ImdbId.Should().Be(1727587);
            release.PublishDate.ToUniversalTime().Should().Be(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc));
            release.Languages.Should().BeEmpty();
            release.IndexerFlags.Should().BeEmpty();
        }

        [Test]
        public void should_keep_the_download_url_the_release_was_grabbed_from()
        {
            Convert(RealRelease()).DownloadUrl.Should().Contain("apikey=" + FakeApiKey).And.Contain("link=ABC");
        }

        [Test]
        public void should_point_the_release_at_the_imported_indexer()
        {
            Convert(RealRelease()).IndexerId.Should().Be(7);
        }

        [Test]
        public void should_set_the_indexer_to_zero_when_no_indexer_matches()
        {
            Convert(Changed(r => r["indexerId"] = 5)).IndexerId.Should().Be(0);
            Convert(Changed(r => r["indexerId"] = 0)).IndexerId.Should().Be(0);
        }

        [Test]
        public void should_leave_the_indexer_alone_when_the_release_has_none()
        {
            Convert(Changed(r => r.Remove("indexerId"))).IndexerId.Should().Be(0);
        }

        [Test]
        public void should_write_a_release_that_reads_back_through_the_applications_own_converter()
        {
            var release = Convert(RealRelease());

            var stored = new EmbeddedDocumentConverter<ReleaseInfo>();
            var parameter = new SQLiteParameter();

            stored.SetValue(parameter, release);

            var again = stored.Parse((string)parameter.Value);

            again.Title.Should().Be(release.Title);
            again.IndexerId.Should().Be(7);
            again.Size.Should().Be(3221225472);
        }

        [Test]
        public void should_turn_language_ids_into_names()
        {
            var release = Convert(Changed(r => r["languages"] = new JArray(1, 5)));

            release.Languages.Should().Equal("English", "Italian");
        }

        [Test]
        public void should_read_languages_stored_as_objects()
        {
            var languages = JArray.Parse("[{\"id\":2,\"name\":\"French\"},{\"name\":\"Flemish\"}]");

            Convert(Changed(r => r["languages"] = languages)).Languages.Should().Equal("French", "Flemish");
        }

        [Test]
        public void should_keep_languages_that_are_already_names()
        {
            Convert(Changed(r => r["languages"] = new JArray("English", "Hindi"))).Languages.Should().Equal("English", "Hindi");
        }

        [Test]
        public void should_translate_language_ids_by_radarrs_numbering()
        {
            // Radarr 26 is Hindi; Sonarr's 26 is Arabic.
            Convert(Changed(r => r["languages"] = new JArray(26))).Languages.Should().Equal("Hindi");
        }

        [Test]
        public void should_refuse_a_language_id_radarr_never_had()
        {
            Action convert = () => Convert(Changed(r => r["languages"] = new JArray(999)));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [TestCase("true")]
        [TestCase("{\"unrelated\":1}")]
        [TestCase("1.5")]
        public void should_refuse_a_language_it_cannot_read(string element)
        {
            Action convert = () => Convert(Changed(r => r["languages"] = new JArray(JToken.Parse(element))));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_refuse_a_languages_value_that_is_not_a_list()
        {
            Action convert = () => Convert(Changed(r => r["languages"] = "English"));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_accept_a_release_with_no_languages_key()
        {
            Convert(Changed(r => r.Remove("languages"))).Languages.Should().BeEmpty();
        }

        [Test]
        public void should_turn_a_flags_number_into_named_flags()
        {
            // Radarr's IndexerFlags: G_Freeleech = 1, G_Scene = 128.
            var release = Convert(Changed(r => r["indexerFlags"] = 129));

            release.IndexerFlags.Should().BeEquivalentTo(new[] { IndexerFlag.FreeLeech, IndexerFlag.Scene });
        }

        [Test]
        public void should_ignore_a_flags_value_of_zero()
        {
            Convert(Changed(r => r["indexerFlags"] = 0)).IndexerFlags.Should().BeEmpty();
        }

        [Test]
        public void should_refuse_flags_that_are_not_a_number()
        {
            Action convert = () => Convert(Changed(r => r["indexerFlags"] = "g_Freeleech"));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [TestCase("not json")]
        [TestCase("[]")]
        [TestCase("\"text\"")]
        [TestCase("")]
        public void should_refuse_a_release_that_is_not_a_json_object(string json)
        {
            Action convert = () => Convert(json);

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_refuse_an_indexer_id_that_is_not_a_number()
        {
            Action convert = () => Convert(Changed(r => r["indexerId"] = "one"));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_refuse_a_release_that_does_not_fit_the_model()
        {
            Action convert = () => Convert(Changed(r => r["size"] = "large"));

            convert.Should().Throw<CorruptSourceDataException>();
        }

        [Test]
        public void should_never_put_the_api_key_in_an_error_message()
        {
            // Every one of these rows carries the key in downloadUrl.
            var broken = new[]
            {
                Changed(r => r["languages"] = new JArray(999)),
                Changed(r => r["indexerFlags"] = "x"),
                Changed(r => r["indexerId"] = "one"),
                Changed(r => r["size"] = "large")
            };

            foreach (var json in broken)
            {
                Action convert = () => Convert(json);

                convert.Should().Throw<CorruptSourceDataException>().Which.ToString().Should().NotContain(FakeApiKey);
            }
        }
    }
}
