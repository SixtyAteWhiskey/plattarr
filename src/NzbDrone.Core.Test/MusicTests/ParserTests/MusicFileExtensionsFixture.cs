using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.ParserTests
{
    [TestFixture]
    public class MusicFileExtensionsFixture : CoreTest
    {
        [TestCase(".flac", 231)]
        [TestCase(".FLAC", 231)]
        [TestCase(".wv", 234)]
        [TestCase(".ape", 233)]
        [TestCase(".wav", 237)]
        [TestCase(".wma", 218)]
        [TestCase(".mp3", 0)]
        [TestCase(".m4a", 0)]
        [TestCase(".ogg", 0)]
        [TestCase(".mkv", 0)]
        [TestCase("", 0)]
        public void should_map_an_extension_to_an_audio_quality_id(string extension, int expectedId)
        {
            MediaFileExtensions.GetQualityForExtension(extension).Id.Should().Be(expectedId);
        }

        [Test]
        public void should_only_map_to_audio_catalog_qualities()
        {
            foreach (var extension in MediaFileExtensions.Extensions)
            {
                AudioQuality.All.Should().Contain(MediaFileExtensions.GetQualityForExtension(extension), extension);
            }
        }

        [Test]
        public void should_list_the_audio_extensions_and_no_video_ones()
        {
            MediaFileExtensions.Extensions.Should().BeEquivalentTo(new[]
            {
                ".mp2", ".mp3", ".m4a", ".m4b", ".m4p", ".ogg", ".oga", ".opus", ".wma", ".wav", ".wv", ".flac",
                ".ape", ".aif", ".aiff", ".aifc"
            });
        }

        [TestCase("Artist - Album - 01 - Track.flac", "Artist - Album - 01 - Track")]
        [TestCase("Artist - Album.nzb", "Artist - Album")]
        [TestCase("Artist - Album.par2", "Artist - Album")]
        [TestCase("Artist - Album.mkv", "Artist - Album.mkv")]
        [TestCase("Artist - Vol.2", "Artist - Vol.2")]
        public void should_strip_only_known_audio_extensions(string title, string expected)
        {
            Music.Parser.Parser.RemoveFileExtension(title).Should().Be(expected);
        }

        [Test]
        public void extensions_should_be_a_copy()
        {
            var first = MediaFileExtensions.Extensions;
            first.Clear();

            MediaFileExtensions.Extensions.Should().NotBeEmpty();
        }
    }
}
