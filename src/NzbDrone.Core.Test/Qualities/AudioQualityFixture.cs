using System;
using System.Data.SQLite;
using System.Linq;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Qualities
{
    [TestFixture]
    public class AudioQualityFixture : CoreTest
    {
        // Catalog IDs are stored in every database; they must never change (docs/catalogs.md).
        // These cases pin every one, with the name Lidarr aec88028 gives the quality.
        private static readonly object[] Pins =
        {
            new object[] { 0, "Unknown" },
            new object[] { 201, "MP3-8" },
            new object[] { 202, "MP3-16" },
            new object[] { 203, "MP3-24" },
            new object[] { 204, "MP3-32" },
            new object[] { 205, "MP3-40" },
            new object[] { 206, "MP3-48" },
            new object[] { 207, "MP3-56" },
            new object[] { 208, "MP3-64" },
            new object[] { 209, "MP3-80" },
            new object[] { 210, "MP3-96" },
            new object[] { 211, "MP3-112" },
            new object[] { 212, "MP3-128" },
            new object[] { 213, "OGG Vorbis Q5" },
            new object[] { 214, "MP3-160" },
            new object[] { 215, "MP3-192" },
            new object[] { 216, "OGG Vorbis Q6" },
            new object[] { 217, "AAC-192" },
            new object[] { 218, "WMA" },
            new object[] { 219, "MP3-224" },
            new object[] { 220, "OGG Vorbis Q7" },
            new object[] { 221, "MP3-VBR-V2" },
            new object[] { 222, "MP3-256" },
            new object[] { 223, "OGG Vorbis Q8" },
            new object[] { 224, "AAC-256" },
            new object[] { 225, "MP3-VBR-V0" },
            new object[] { 226, "AAC-VBR" },
            new object[] { 227, "MP3-320" },
            new object[] { 228, "OGG Vorbis Q9" },
            new object[] { 229, "AAC-320" },
            new object[] { 230, "OGG Vorbis Q10" },
            new object[] { 231, "FLAC" },
            new object[] { 232, "ALAC" },
            new object[] { 233, "APE" },
            new object[] { 234, "WavPack" },
            new object[] { 235, "FLAC 24bit" },
            new object[] { 236, "ALAC 24bit" },
            new object[] { 237, "WAV" }
        };

        // Lidarr aec88028's default quality definitions, copied from its Quality.cs: name, weight,
        // group weight, group name, min, max and preferred size.
        private static readonly object[] LidarrDefaults =
        {
            new object[] { "Unknown", 1, 1, null, 0d, 350d, 195d },
            new object[] { "MP3-8", 2, 2, "Trash Quality Lossy", 0d, 10d, 5d },
            new object[] { "MP3-16", 3, 2, "Trash Quality Lossy", 0d, 20d, 15d },
            new object[] { "MP3-24", 4, 2, "Trash Quality Lossy", 0d, 30d, 25d },
            new object[] { "MP3-32", 5, 2, "Trash Quality Lossy", 0d, 40d, 35d },
            new object[] { "MP3-40", 6, 2, "Trash Quality Lossy", 0d, 45d, 40d },
            new object[] { "MP3-48", 7, 2, "Trash Quality Lossy", 0d, 55d, 50d },
            new object[] { "MP3-56", 8, 2, "Trash Quality Lossy", 0d, 65d, 60d },
            new object[] { "MP3-64", 9, 2, "Trash Quality Lossy", 0d, 75d, 70d },
            new object[] { "MP3-80", 10, 2, "Trash Quality Lossy", 0d, 95d, 90d },
            new object[] { "MP3-96", 11, 3, "Poor Quality Lossy", 0d, 110d, 95d },
            new object[] { "MP3-112", 12, 3, "Poor Quality Lossy", 0d, 125d, 95d },
            new object[] { "MP3-128", 13, 3, "Poor Quality Lossy", 0d, 140d, 95d },
            new object[] { "OGG Vorbis Q5", 14, 3, "Poor Quality Lossy", 0d, 175d, 95d },
            new object[] { "MP3-160", 14, 3, "Poor Quality Lossy", 0d, 175d, 95d },
            new object[] { "MP3-192", 15, 4, "Low Quality Lossy", 0d, 210d, 95d },
            new object[] { "OGG Vorbis Q6", 15, 4, "Low Quality Lossy", 0d, 210d, 95d },
            new object[] { "AAC-192", 15, 4, "Low Quality Lossy", 0d, 210d, 95d },
            new object[] { "WMA", 15, 4, "Low Quality Lossy", 0d, 350d, 195d },
            new object[] { "MP3-224", 16, 4, "Low Quality Lossy", 0d, 245d, 95d },
            new object[] { "OGG Vorbis Q7", 17, 5, "Mid Quality Lossy", 0d, 245d, 95d },
            new object[] { "MP3-VBR-V2", 18, 5, "Mid Quality Lossy", 0d, 280d, 95d },
            new object[] { "MP3-256", 18, 5, "Mid Quality Lossy", 0d, 280d, 95d },
            new object[] { "OGG Vorbis Q8", 18, 5, "Mid Quality Lossy", 0d, 280d, 95d },
            new object[] { "AAC-256", 18, 5, "Mid Quality Lossy", 0d, 280d, 95d },
            new object[] { "MP3-VBR-V0", 19, 6, "High Quality Lossy", 0d, 350d, 195d },
            new object[] { "AAC-VBR", 19, 6, "High Quality Lossy", 0d, 350d, 195d },
            new object[] { "MP3-320", 20, 6, "High Quality Lossy", 0d, 350d, 195d },
            new object[] { "OGG Vorbis Q9", 20, 6, "High Quality Lossy", 0d, 350d, 195d },
            new object[] { "AAC-320", 20, 6, "High Quality Lossy", 0d, 350d, 195d },
            new object[] { "OGG Vorbis Q10", 21, 6, "High Quality Lossy", 0d, 550d, 295d },
            new object[] { "FLAC", 22, 7, "Lossless", 0d, null, 895d },
            new object[] { "ALAC", 22, 7, "Lossless", 0d, null, 895d },
            new object[] { "APE", 22, 7, "Lossless", 0d, null, 895d },
            new object[] { "WavPack", 22, 7, "Lossless", 0d, null, 895d },
            new object[] { "FLAC 24bit", 23, 7, "Lossless", 0d, null, 895d },
            new object[] { "ALAC 24bit", 23, 7, "Lossless", 0d, null, 895d },
            new object[] { "WAV", 24, 8, null, 0d, null, 895d }
        };

        [TestCaseSource(nameof(Pins))]
        public void catalog_id_should_never_change(int id, string name)
        {
            AudioQuality.FindById(id).Name.Should().Be(name);
            AudioQuality.All.Single(q => q.Name == name).Id.Should().Be(id);
        }

        [Test]
        public void catalog_should_hold_exactly_the_pinned_qualities()
        {
            AudioQuality.All.Select(q => q.Id).Should().BeEquivalentTo(Pins.Cast<object[]>().Select(c => (int)c[0]));
        }

        [Test]
        public void ids_should_be_unique_and_sit_outside_the_video_range()
        {
            AudioQuality.All.Select(q => q.Id).Should().OnlyHaveUniqueItems();
            AudioQuality.All.Select(q => q.Name).Should().OnlyHaveUniqueItems();

            foreach (var quality in AudioQuality.All.Where(q => q.Id != 0))
            {
                quality.Id.Should().BeInRange(201, 399, quality.Name);
            }

            AudioQuality.All.Select(q => q.Id).Intersect(Quality.All.Select(q => q.Id)).Should().BeEquivalentTo(new[] { 0 }, "only Unknown is in both catalogs");
        }

        [TestCase(1)]
        [TestCase(-1)]
        [TestCase(200)]
        [TestCase(238)]
        [TestCase(399)]
        public void unknown_id_should_throw(int id)
        {
            Assert.Throws<ArgumentException>(() => AudioQuality.FindById(id));
        }

        [Test]
        public void explicit_conversions_should_round_trip()
        {
            foreach (var quality in AudioQuality.All)
            {
                ((AudioQuality)(int)quality).Should().Be(quality);
            }
        }

        [TestCaseSource(nameof(LidarrDefaults))]
        public void default_definition_should_match_lidarr(string name, int weight, int groupWeight, string groupName, double? min, double? max, double? preferred)
        {
            var definition = AudioQuality.DefaultQualityDefinitions.Single(d => d.Quality.Name == name);

            definition.Title.Should().Be(name);
            definition.Weight.Should().Be(weight);
            definition.GroupWeight.Should().Be(groupWeight);
            definition.GroupName.Should().Be(groupName);
            definition.MinSize.Should().Be(min);
            definition.MaxSize.Should().Be(max);
            definition.PreferredSize.Should().Be(preferred);
        }

        [Test]
        public void every_quality_should_have_exactly_one_default_definition()
        {
            AudioQuality.DefaultQualityDefinitions.Select(d => d.Quality).Should().BeEquivalentTo(AudioQuality.All);
            AudioQuality.DefaultQualityDefinitions.Should().HaveCount(AudioQuality.All.Count);
        }

        [Test]
        public void ids_should_follow_the_default_weight_order()
        {
            var byId = AudioQuality.DefaultQualityDefinitions.Where(d => d.Quality.Id != 0).OrderBy(d => d.Quality.Id).ToList();

            byId.Select(d => d.Weight).Should().BeInAscendingOrder();
        }

        [Test]
        public void a_group_name_should_have_one_group_weight()
        {
            var grouped = AudioQuality.DefaultQualityDefinitions.Where(d => d.GroupName != null).GroupBy(d => d.GroupName);

            foreach (var group in grouped)
            {
                group.Select(d => d.GroupWeight).Distinct().Should().HaveCount(1, group.Key);
            }
        }

        [Test]
        public void quality_should_be_stored_as_its_catalog_id()
        {
            var param = new SQLiteParameter();
            var converter = new EmbeddedDocumentConverter<AudioQualityModel>(new AudioQualityIntConverter());

            converter.SetValue(param, new AudioQualityModel(AudioQuality.FLAC_24, new Revision(2, 1)));

            using var json = JsonDocument.Parse((string)param.Value);
            json.RootElement.GetProperty("quality").GetInt32().Should().Be(235);
            json.RootElement.GetProperty("revision").GetProperty("version").GetInt32().Should().Be(2);
            json.RootElement.GetProperty("revision").GetProperty("real").GetInt32().Should().Be(1);
        }

        [Test]
        public void stored_quality_should_read_back_as_the_same_audio_quality()
        {
            var param = new SQLiteParameter();
            var converter = new EmbeddedDocumentConverter<AudioQualityModel>(new AudioQualityIntConverter());

            foreach (var quality in AudioQuality.All)
            {
                var model = new AudioQualityModel(quality, new Revision(3, 2));

                converter.SetValue(param, model);

                converter.Parse(param.Value).Should().Be(model);
            }
        }

        [Test]
        public void dapper_handler_should_store_the_id_and_null_as_unknown()
        {
            var param = new SQLiteParameter();
            var handler = new DapperAudioQualityIntConverter();

            handler.SetValue(param, AudioQuality.ALAC);
            param.Value.Should().Be(232);
            handler.Parse(232).Should().Be(AudioQuality.ALAC);

            handler.SetValue(param, null);
            param.Value.Should().Be(0);
            handler.Parse(0).Should().Be(AudioQuality.Unknown);
        }

        [Test]
        public void display_model_should_keep_id_name_and_revision_and_default_the_video_attributes()
        {
            var display = new AudioQualityModel(AudioQuality.MP3_320, new Revision(2, 1, true)).ToDisplayModel();

            display.Quality.Id.Should().Be(227);
            display.Quality.Name.Should().Be("MP3-320");
            display.Quality.Source.Should().Be(QualitySource.UNKNOWN);
            display.Quality.Resolution.Should().Be(0);
            display.Quality.Modifier.Should().Be(Modifier.NONE);
            display.Revision.Version.Should().Be(2);
            display.Revision.Real.Should().Be(1);
        }

        [Test]
        public void display_model_should_convert_back_for_every_audio_quality()
        {
            foreach (var quality in AudioQuality.All)
            {
                var model = new AudioQualityModel(quality, new Revision(2, 1));

                AudioQualityModel.FromDisplayModel(model.ToDisplayModel()).Should().Be(model);
            }
        }

        [Test]
        public void display_model_of_a_video_quality_should_throw()
        {
            Assert.Throws<ArgumentException>(() => AudioQualityModel.FromDisplayModel(new QualityModel(Quality.Bluray1080p)));
        }

        [Test]
        public void display_model_of_nothing_should_be_unknown()
        {
            AudioQualityModel.FromDisplayModel(null).Quality.Should().Be(AudioQuality.Unknown);
            AudioQualityModel.FromDisplayModel(new QualityModel(Quality.Unknown)).Quality.Should().Be(AudioQuality.Unknown);
        }

        [Test]
        public void models_should_compare_by_quality_id_and_revision()
        {
            new AudioQualityModel(AudioQuality.FLAC, new Revision(1)).Should().Be(new AudioQualityModel(AudioQuality.FLAC, new Revision(1)));
            new AudioQualityModel(AudioQuality.FLAC, new Revision(1)).Should().NotBe(new AudioQualityModel(AudioQuality.ALAC, new Revision(1)));
            new AudioQualityModel(AudioQuality.FLAC, new Revision(1)).Should().NotBe(new AudioQualityModel(AudioQuality.FLAC, new Revision(2)));
        }
    }
}
