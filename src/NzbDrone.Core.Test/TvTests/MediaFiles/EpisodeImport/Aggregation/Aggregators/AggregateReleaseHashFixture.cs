using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles.EpisodeImport.Aggregation.Aggregators;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests.MediaFiles.EpisodeImport.Aggregation.Aggregators
{
    [TestFixture]
    public class AggregateReleaseHashFixture : CoreTest<AggregateReleaseHash>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew().Build();
        }

        [Test]
        public void should_prefer_file()
        {
            var fileEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC) [ABCDEF01]");
            var folderEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 [12345678]");
            var downloadClientEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC) [ABCD1234]");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                DownloadClientEpisodeInfo = downloadClientEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01\Series.Title.S01E01.mkv".AsOsAgnostic(),
                Series = _series
            };

            Subject.Aggregate(localEpisode, null);

            localEpisode.ReleaseHash.Should().Be("ABCDEF01");
        }

        [Test]
        public void should_fallback_to_downloadclient()
        {
            var fileEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC)");
            var downloadClientEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC) [ABCD1234]");
            var folderEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 [12345678]");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                DownloadClientEpisodeInfo = downloadClientEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01\Series.Title.S01E01.WEB-DL.mkv".AsOsAgnostic(),
                Series = _series
            };

            Subject.Aggregate(localEpisode, null);

            localEpisode.ReleaseHash.Should().Be("ABCD1234");
        }

        [Test]
        public void should_fallback_to_folder()
        {
            var fileEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC)");
            var downloadClientEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 (1280x720 10bit AAC)");
            var folderEpisodeInfo = Core.Tv.Parser.Parser.ParseTitle("[DHD] Series Title! - 08 [12345678]");
            var localEpisode = new LocalEpisode
            {
                FileEpisodeInfo = fileEpisodeInfo,
                FolderEpisodeInfo = folderEpisodeInfo,
                DownloadClientEpisodeInfo = downloadClientEpisodeInfo,
                Path = @"C:\Test\Unsorted TV\Series.Title.S01\Series.Title.S01E01.WEB-DL.mkv".AsOsAgnostic(),
                Series = _series
            };

            Subject.Aggregate(localEpisode, null);

            localEpisode.ReleaseHash.Should().Be("12345678");
        }
    }
}
