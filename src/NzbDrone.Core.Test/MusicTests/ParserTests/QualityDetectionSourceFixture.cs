using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.ParserTests
{
    // Lidarr's QualityDetectionSource started at Name, so a name-based result needed no assignment.
    // The shared enum starts at Unknown, so the Music parser sets every source itself.
    [TestFixture]
    public class QualityDetectionSourceFixture : CoreTest
    {
        [TestCase("Artist - Album [FLAC]")]
        [TestCase("Artist.Title-Album.Title.2017.WAVPACK-aAF")]
        public void should_report_name_when_the_quality_came_from_the_title(string title)
        {
            var result = QualityParser.ParseQuality(title, null, 0);

            result.Quality.Should().NotBe(AudioQuality.Unknown);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.Name);
            result.RevisionDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        // Lidarr tries the extension whenever the title gave no quality, and records the source even
        // when the extension gives none either. Kept as ported.
        [Test]
        public void should_report_extension_when_the_title_has_no_codec_at_all()
        {
            var result = QualityParser.ParseQuality("Artist - Album", null, 0);

            result.Quality.Should().Be(AudioQuality.Unknown);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.Extension);
        }

        [TestCase(null, null)]
        [TestCase("", "")]
        [TestCase("  ", null)]
        public void should_report_name_for_an_empty_title(string name, string desc)
        {
            var result = QualityParser.ParseQuality(name, desc, 0);

            result.Quality.Should().Be(AudioQuality.Unknown);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        [Test]
        public void should_report_tag_lib_when_the_description_gives_the_quality()
        {
            var result = QualityParser.ParseQuality("track.mp3", "MPEG Version 1 Audio, Layer 3", 320);

            result.Quality.Should().Be(AudioQuality.MP3_320);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.TagLib);
        }

        [Test]
        public void should_fall_back_to_the_title_when_the_description_gives_no_quality()
        {
            var result = QualityParser.ParseQuality("Artist - Album [FLAC]", "unknown codec", 0);

            result.Quality.Should().Be(AudioQuality.FLAC);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.Name);
        }

        [Test]
        public void should_report_extension_when_only_the_extension_gives_the_quality()
        {
            // ".wv" and ".flac" are also codec words, so they are found in the name. ".ape" is only
            // a codec inside brackets, so a bare extension has to be read as one.
            var result = QualityParser.ParseQuality("01. Track.ape", null, 0);

            result.Quality.Should().Be(AudioQuality.APE);
            result.QualityDetectionSource.Should().Be(QualityDetectionSource.Extension);
        }
    }
}
