using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class EpisodeFileRepositoryFixture : DbTest<MediaFileRepository, EpisodeFile>
    {
        private Series _series;
        private Series _other;

        [SetUp]
        public void Setup()
        {
            var seriesRepository = Mocker.Resolve<SeriesRepository>();

            _series = TvTestData.NewSeries(81189, "Breaking Bad");
            _other = TvTestData.NewSeries(121361, "Game Of Thrones");

            seriesRepository.Insert(_series);
            seriesRepository.Insert(_other);
        }

        [Test]
        public void should_round_trip_every_episode_file_column()
        {
            var file = TvTestData.NewEpisodeFile(_series.Id, 1, "Season 1/S01E01.mkv");

            Subject.Insert(file);

            Subject.Get(file.Id).Should().BeEquivalentTo(
                file,
                o => o.Excluding(f => f.Path)
                      .Excluding(f => f.Episodes)
                      .Excluding(f => f.Series));
        }

        [Test]
        public void should_store_media_info_as_json_and_read_it_back_with_streams()
        {
            var file = TvTestData.NewEpisodeFile(_series.Id, 1, "S01E01.mkv");

            Subject.Insert(file);

            var info = Subject.Get(file.Id).MediaInfo;

            info.AudioStreams.Select(s => s.Language).Should().Equal("eng", "fra");
            info.SubtitleStreams.Single().HearingImpaired.Should().BeTrue();
            info.VideoHdrFormat.Should().Be(NzbDrone.Core.Tv.MediaFiles.MediaInfo.HdrFormat.Hdr10);
        }

        [Test]
        public void should_find_files_by_series_season_and_path()
        {
            Subject.Insert(TvTestData.NewEpisodeFile(_series.Id, 1, "a.mkv"));
            Subject.Insert(TvTestData.NewEpisodeFile(_series.Id, 2, "b.mkv"));
            Subject.Insert(TvTestData.NewEpisodeFile(_other.Id, 1, "a.mkv"));

            Subject.GetFilesBySeries(_series.Id).Should().HaveCount(2);
            Subject.GetFilesBySeriesIds(new[] { _series.Id, _other.Id }.ToList()).Should().HaveCount(3);
            Subject.GetFilesBySeason(_series.Id, 2).Single().RelativePath.Should().Be("b.mkv");
            Subject.GetFilesWithRelativePath(_other.Id, "a.mkv").Should().ContainSingle().Which.SeriesId.Should().Be(_other.Id);
            Subject.GetFilesWithRelativePath(_series.Id, "missing.mkv").Should().BeEmpty();
        }

        [Test]
        public void should_find_files_without_media_info()
        {
            var withInfo = TvTestData.NewEpisodeFile(_series.Id, 1, "info.mkv");
            var withoutInfo = TvTestData.NewEpisodeFile(_series.Id, 1, "bare.mkv");
            withoutInfo.MediaInfo = null;

            Subject.Insert(withInfo);
            Subject.Insert(withoutInfo);

            Subject.GetFilesWithoutMediaInfo().Select(f => f.RelativePath).Should().Equal("bare.mkv");
        }

        [Test]
        public void should_load_the_series_and_episodes_of_a_file_lazily()
        {
            var episodeRepository = Mocker.Resolve<EpisodeRepository>();
            var file = TvTestData.NewEpisodeFile(_series.Id, 1, "S01E01.mkv", Quality.HDTV720p);
            Subject.Insert(file);

            episodeRepository.Insert(TvTestData.NewEpisode(_series.Id, 1, 1, null, true, file.Id));
            episodeRepository.Insert(TvTestData.NewEpisode(_series.Id, 1, 2, null, true, file.Id));

            var stored = Subject.Get(file.Id);

            stored.Series.Value.Title.Should().Be("Breaking Bad");
            stored.Episodes.Value.Select(e => e.EpisodeNumber).Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Test]
        public void should_delete_the_files_of_the_given_series_only()
        {
            Subject.Insert(TvTestData.NewEpisodeFile(_series.Id, 1, "a.mkv"));
            Subject.Insert(TvTestData.NewEpisodeFile(_other.Id, 1, "b.mkv"));

            Subject.DeleteForSeries(new[] { _series.Id }.ToList());

            Subject.All().Single().SeriesId.Should().Be(_other.Id);
        }
    }
}
