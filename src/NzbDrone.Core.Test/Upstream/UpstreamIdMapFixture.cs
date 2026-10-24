using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Languages.Upstream;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Qualities.Upstream;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Upstream
{
    // The cases are the upstream apps' own ID and name tables, copied from
    // Radarr 14afe52, Sonarr 7ccafd3 and Lidarr aec88028, not from the maps under test. A map entry
    // is right when the catalog entry it points at has the upstream name.
    [TestFixture]
    public class UpstreamIdMapFixture : CoreTest
    {
        private static readonly object[] RadarrQualities =
        {
            new object[] { 0, "Unknown" },
            new object[] { 1, "SDTV" },
            new object[] { 2, "DVD" },
            new object[] { 3, "WEBDL-1080p" },
            new object[] { 4, "HDTV-720p" },
            new object[] { 5, "WEBDL-720p" },
            new object[] { 6, "Bluray-720p" },
            new object[] { 7, "Bluray-1080p" },
            new object[] { 8, "WEBDL-480p" },
            new object[] { 9, "HDTV-1080p" },
            new object[] { 10, "Raw-HD" },
            new object[] { 12, "WEBRip-480p" },
            new object[] { 14, "WEBRip-720p" },
            new object[] { 15, "WEBRip-1080p" },
            new object[] { 16, "HDTV-2160p" },
            new object[] { 17, "WEBRip-2160p" },
            new object[] { 18, "WEBDL-2160p" },
            new object[] { 19, "Bluray-2160p" },
            new object[] { 20, "Bluray-480p" },
            new object[] { 21, "Bluray-576p" },
            new object[] { 22, "BR-DISK" },
            new object[] { 23, "DVD-R" },
            new object[] { 24, "WORKPRINT" },
            new object[] { 25, "CAM" },
            new object[] { 26, "TELESYNC" },
            new object[] { 27, "TELECINE" },
            new object[] { 28, "DVDSCR" },
            new object[] { 29, "REGIONAL" },
            new object[] { 30, "Remux-1080p" },
            new object[] { 31, "Remux-2160p" },
        };

        private static readonly object[] SonarrQualities =
        {
            new object[] { 0, "Unknown" },
            new object[] { 1, "SDTV" },
            new object[] { 2, "DVD" },
            new object[] { 3, "WEBDL-1080p" },
            new object[] { 4, "HDTV-720p" },
            new object[] { 5, "WEBDL-720p" },
            new object[] { 6, "Bluray-720p" },
            new object[] { 7, "Bluray-1080p" },
            new object[] { 8, "WEBDL-480p" },
            new object[] { 9, "HDTV-1080p" },
            new object[] { 10, "Raw-HD" },
            new object[] { 12, "WEBRip-480p" },
            new object[] { 13, "Bluray-480p" },
            new object[] { 14, "WEBRip-720p" },
            new object[] { 15, "WEBRip-1080p" },
            new object[] { 16, "HDTV-2160p" },
            new object[] { 17, "WEBRip-2160p" },
            new object[] { 18, "WEBDL-2160p" },
            new object[] { 19, "Bluray-2160p" },
            new object[] { 20, "Bluray-1080p Remux" },
            new object[] { 21, "Bluray-2160p Remux" },
            new object[] { 22, "Bluray-576p" },
        };

        private static readonly object[] LidarrQualities =
        {
            new object[] { 0, "Unknown" },
            new object[] { 1, "MP3-192" },
            new object[] { 2, "MP3-VBR-V0" },
            new object[] { 3, "MP3-256" },
            new object[] { 4, "MP3-320" },
            new object[] { 5, "MP3-160" },
            new object[] { 6, "FLAC" },
            new object[] { 7, "ALAC" },
            new object[] { 8, "MP3-VBR-V2" },
            new object[] { 9, "AAC-192" },
            new object[] { 10, "AAC-256" },
            new object[] { 11, "AAC-320" },
            new object[] { 12, "AAC-VBR" },
            new object[] { 13, "WAV" },
            new object[] { 14, "OGG Vorbis Q10" },
            new object[] { 15, "OGG Vorbis Q9" },
            new object[] { 16, "OGG Vorbis Q8" },
            new object[] { 17, "OGG Vorbis Q7" },
            new object[] { 18, "OGG Vorbis Q6" },
            new object[] { 19, "OGG Vorbis Q5" },
            new object[] { 20, "WMA" },
            new object[] { 21, "FLAC 24bit" },
            new object[] { 22, "MP3-128" },
            new object[] { 23, "MP3-96" },
            new object[] { 24, "MP3-80" },
            new object[] { 25, "MP3-64" },
            new object[] { 26, "MP3-56" },
            new object[] { 27, "MP3-48" },
            new object[] { 28, "MP3-40" },
            new object[] { 29, "MP3-32" },
            new object[] { 30, "MP3-24" },
            new object[] { 31, "MP3-16" },
            new object[] { 32, "MP3-8" },
            new object[] { 33, "MP3-112" },
            new object[] { 34, "MP3-224" },
            new object[] { 35, "APE" },
            new object[] { 36, "WavPack" },
            new object[] { 37, "ALAC 24bit" }
        };

        private static readonly object[] RadarrLanguages =
        {
            new object[] { -2, "Original" },
            new object[] { -1, "Any" },
            new object[] { 0, "Unknown" },
            new object[] { 1, "English" },
            new object[] { 2, "French" },
            new object[] { 3, "Spanish" },
            new object[] { 4, "German" },
            new object[] { 5, "Italian" },
            new object[] { 6, "Danish" },
            new object[] { 7, "Dutch" },
            new object[] { 8, "Japanese" },
            new object[] { 9, "Icelandic" },
            new object[] { 10, "Chinese" },
            new object[] { 11, "Russian" },
            new object[] { 12, "Polish" },
            new object[] { 13, "Vietnamese" },
            new object[] { 14, "Swedish" },
            new object[] { 15, "Norwegian" },
            new object[] { 16, "Finnish" },
            new object[] { 17, "Turkish" },
            new object[] { 18, "Portuguese" },
            new object[] { 19, "Flemish" },
            new object[] { 20, "Greek" },
            new object[] { 21, "Korean" },
            new object[] { 22, "Hungarian" },
            new object[] { 23, "Hebrew" },
            new object[] { 24, "Lithuanian" },
            new object[] { 25, "Czech" },
            new object[] { 26, "Hindi" },
            new object[] { 27, "Romanian" },
            new object[] { 28, "Thai" },
            new object[] { 29, "Bulgarian" },
            new object[] { 30, "Portuguese (Brazil)" },
            new object[] { 31, "Arabic" },
            new object[] { 32, "Ukrainian" },
            new object[] { 33, "Persian" },
            new object[] { 34, "Bengali" },
            new object[] { 35, "Slovak" },
            new object[] { 36, "Latvian" },
            new object[] { 37, "Spanish (Latino)" },
            new object[] { 38, "Catalan" },
            new object[] { 39, "Croatian" },
            new object[] { 40, "Serbian" },
            new object[] { 41, "Bosnian" },
            new object[] { 42, "Estonian" },
            new object[] { 43, "Tamil" },
            new object[] { 44, "Indonesian" },
            new object[] { 45, "Telugu" },
            new object[] { 46, "Macedonian" },
            new object[] { 47, "Slovenian" },
            new object[] { 48, "Malayalam" },
            new object[] { 49, "Kannada" },
            new object[] { 50, "Albanian" },
            new object[] { 51, "Afrikaans" },
            new object[] { 52, "Marathi" },
            new object[] { 53, "Tagalog" },
            new object[] { 54, "Urdu" },
            new object[] { 55, "Romansh" },
            new object[] { 56, "Mongolian" },
            new object[] { 57, "Georgian" },
        };

        private static readonly object[] SonarrLanguages =
        {
            new object[] { -2, "Original" },
            new object[] { 0, "Unknown" },
            new object[] { 1, "English" },
            new object[] { 2, "French" },
            new object[] { 3, "Spanish" },
            new object[] { 4, "German" },
            new object[] { 5, "Italian" },
            new object[] { 6, "Danish" },
            new object[] { 7, "Dutch" },
            new object[] { 8, "Japanese" },
            new object[] { 9, "Icelandic" },
            new object[] { 10, "Chinese" },
            new object[] { 11, "Russian" },
            new object[] { 12, "Polish" },
            new object[] { 13, "Vietnamese" },
            new object[] { 14, "Swedish" },
            new object[] { 15, "Norwegian" },
            new object[] { 16, "Finnish" },
            new object[] { 17, "Turkish" },
            new object[] { 18, "Portuguese" },
            new object[] { 19, "Flemish" },
            new object[] { 20, "Greek" },
            new object[] { 21, "Korean" },
            new object[] { 22, "Hungarian" },
            new object[] { 23, "Hebrew" },
            new object[] { 24, "Lithuanian" },
            new object[] { 25, "Czech" },
            new object[] { 26, "Arabic" },
            new object[] { 27, "Hindi" },
            new object[] { 28, "Bulgarian" },
            new object[] { 29, "Malayalam" },
            new object[] { 30, "Ukrainian" },
            new object[] { 31, "Slovak" },
            new object[] { 32, "Thai" },
            new object[] { 33, "Portuguese (Brazil)" },
            new object[] { 34, "Spanish (Latino)" },
            new object[] { 35, "Romanian" },
            new object[] { 36, "Latvian" },
            new object[] { 37, "Persian" },
            new object[] { 38, "Catalan" },
            new object[] { 39, "Croatian" },
            new object[] { 40, "Serbian" },
            new object[] { 41, "Bosnian" },
            new object[] { 42, "Estonian" },
            new object[] { 43, "Tamil" },
            new object[] { 44, "Indonesian" },
            new object[] { 45, "Macedonian" },
            new object[] { 46, "Slovenian" },
            new object[] { 47, "Azerbaijani" },
            new object[] { 48, "Uzbek" },
            new object[] { 49, "Malay" },
            new object[] { 50, "Urdu" },
            new object[] { 51, "Romansh" },
            new object[] { 52, "Georgian" },
            new object[] { 53, "Irish" },
        };

        // Sonarr names the two remuxes differently; everything else matches by name.
        private static string CatalogName(string sonarrName) => sonarrName switch
        {
            "Bluray-1080p Remux" => "Remux-1080p",
            "Bluray-2160p Remux" => "Remux-2160p",
            _ => sonarrName
        };

        [TestCaseSource(nameof(RadarrQualities))]
        public void radarr_quality_should_map_to_catalog_entry_with_same_name(int radarrId, string name)
        {
            var catalog = Quality.FindById(RadarrQualityIds.ToCatalog(radarrId));

            catalog.Name.Should().Be(name);
            RadarrQualityIds.TryToUpstream(catalog.Id, out var back).Should().BeTrue();
            back.Should().Be(radarrId);
        }

        [TestCaseSource(nameof(SonarrQualities))]
        public void sonarr_quality_should_map_to_catalog_entry_with_same_name(int sonarrId, string name)
        {
            var catalog = Quality.FindById(SonarrQualityIds.ToCatalog(sonarrId));

            catalog.Name.Should().Be(CatalogName(name));
            SonarrQualityIds.TryToUpstream(catalog.Id, out var back).Should().BeTrue();
            back.Should().Be(sonarrId);
        }

        [TestCaseSource(nameof(LidarrQualities))]
        public void lidarr_quality_should_map_to_audio_catalog_entry_with_same_name(int lidarrId, string name)
        {
            var catalog = AudioQuality.FindById(LidarrQualityIds.ToCatalog(lidarrId));

            catalog.Name.Should().Be(name);
            LidarrQualityIds.TryToUpstream(catalog.Id, out var back).Should().BeTrue();
            back.Should().Be(lidarrId);
        }

        [TestCaseSource(nameof(RadarrLanguages))]
        public void radarr_language_should_map_to_catalog_entry_with_same_name(int radarrId, string name)
        {
            var catalog = Language.FindById(RadarrLanguageIds.ToCatalog(radarrId));

            catalog.Name.Should().Be(name);
            RadarrLanguageIds.TryToUpstream(catalog.Id, out var back).Should().BeTrue();
            back.Should().Be(radarrId);
        }

        [TestCaseSource(nameof(SonarrLanguages))]
        public void sonarr_language_should_map_to_catalog_entry_with_same_name(int sonarrId, string name)
        {
            var catalog = Language.FindById(SonarrLanguageIds.ToCatalog(sonarrId));

            catalog.Name.Should().Be(name);
            SonarrLanguageIds.TryToUpstream(catalog.Id, out var back).Should().BeTrue();
            back.Should().Be(sonarrId);
        }

        [Test]
        public void maps_should_cover_every_upstream_entry_and_nothing_else()
        {
            RadarrQualityIds.UpstreamToCatalog.Keys.Should().BeEquivalentTo(RadarrQualities.Cast<object[]>().Select(c => (int)c[0]));
            SonarrQualityIds.UpstreamToCatalog.Keys.Should().BeEquivalentTo(SonarrQualities.Cast<object[]>().Select(c => (int)c[0]));
            LidarrQualityIds.UpstreamToCatalog.Keys.Should().BeEquivalentTo(LidarrQualities.Cast<object[]>().Select(c => (int)c[0]));
            RadarrLanguageIds.UpstreamToCatalog.Keys.Should().BeEquivalentTo(RadarrLanguages.Cast<object[]>().Select(c => (int)c[0]));
            SonarrLanguageIds.UpstreamToCatalog.Keys.Should().BeEquivalentTo(SonarrLanguages.Cast<object[]>().Select(c => (int)c[0]));
        }

        [Test]
        public void every_catalog_entry_should_have_an_upstream_source()
        {
            foreach (var quality in Quality.All)
            {
                (RadarrQualityIds.TryToUpstream(quality.Id, out _) || SonarrQualityIds.TryToUpstream(quality.Id, out _)).Should().BeTrue(quality.Name);
            }

            foreach (var quality in AudioQuality.All)
            {
                LidarrQualityIds.TryToUpstream(quality.Id, out _).Should().BeTrue(quality.Name);
            }

            foreach (var language in Language.All)
            {
                (RadarrLanguageIds.TryToUpstream(language.Id, out _) || SonarrLanguageIds.TryToUpstream(language.Id, out _)).Should().BeTrue(language.Name);
            }
        }

        [Test]
        public void sonarr_should_have_no_id_for_movie_only_qualities()
        {
            SonarrQualityIds.TryToUpstream(Quality.CAM.Id, out _).Should().BeFalse();
        }

        [Test]
        public void lidarr_should_have_no_id_for_video_qualities()
        {
            foreach (var quality in Quality.All.Where(q => q.Id != 0))
            {
                LidarrQualityIds.TryToUpstream(quality.Id, out _).Should().BeFalse(quality.Name);
            }
        }

        [Test]
        public void radarr_and_sonarr_should_have_no_id_for_audio_qualities()
        {
            foreach (var quality in AudioQuality.All.Where(q => q.Id != 0))
            {
                RadarrQualityIds.TryToUpstream(quality.Id, out _).Should().BeFalse(quality.Name);
                SonarrQualityIds.TryToUpstream(quality.Id, out _).Should().BeFalse(quality.Name);
            }
        }

        [Test]
        public void unknown_upstream_id_should_throw()
        {
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => RadarrQualityIds.ToCatalog(11));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => SonarrQualityIds.ToCatalog(11));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => LidarrQualityIds.ToCatalog(38));
        }
    }
}
