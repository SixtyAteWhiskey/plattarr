using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.MediaFiles;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class EpisodeRepositoryFixture : DbTest<EpisodeRepository, Episode>
    {
        private static readonly DateTime Past = DateTime.UtcNow.AddDays(-30);

        private Series _series;
        private Series _other;
        private SeriesRepository _seriesRepository;
        private MediaFileRepository _fileRepository;

        [SetUp]
        public void Setup()
        {
            _seriesRepository = Mocker.Resolve<SeriesRepository>();
            _fileRepository = Mocker.Resolve<MediaFileRepository>();

            _series = TvTestData.NewSeries(81189, "Breaking Bad", 1);
            _series.Runtime = 45;
            _other = TvTestData.NewSeries(121361, "Game Of Thrones", 1);

            _seriesRepository.Insert(_series);
            _seriesRepository.Insert(_other);
        }

        private Episode Add(int season, int number, DateTime? airDate = null, bool monitored = true, int fileId = 0, Series series = null)
        {
            var episode = TvTestData.NewEpisode((series ?? _series).Id, season, number, airDate ?? Past, monitored, fileId);

            Subject.Insert(episode);

            return episode;
        }

        private EpisodeFile AddFile(int season, string path, Quality quality, Series series = null)
        {
            var file = TvTestData.NewEpisodeFile((series ?? _series).Id, season, path, quality);

            _fileRepository.Insert(file);

            return file;
        }

        private static PagingSpec<Episode> Page() => new PagingSpec<Episode>
        {
            Page = 1,
            PageSize = 50,
            SortKey = "airDateUtc",
            SortDirection = SortDirection.Ascending
        };

        [Test]
        public void should_round_trip_every_episode_column()
        {
            var episode = TvTestData.NewEpisode(_series.Id, 1, 2, Past);

            Subject.Insert(episode);

            Subject.Get(episode.Id).Should().BeEquivalentTo(
                episode,
                o => o.Excluding(e => e.Series)
                      .Excluding(e => e.EpisodeFile)
                      .Excluding(e => e.SeriesTitle)
                      .Excluding(e => e.HasFile)
                      .Excluding(e => e.AbsoluteEpisodeNumberAdded)
                      .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime>());
        }

        [Test]
        public void should_find_episodes_by_number_date_scene_and_file()
        {
            var file = AddFile(1, "S01E02.mkv", Quality.HDTV720p);
            var first = Add(1, 1);
            var second = Add(1, 2, Past.AddDays(1), true, file.Id);
            Add(1, 3, Past.AddDays(1), true, file.Id);
            Add(1, 1, series: _other);

            Subject.Find(_series.Id, 1, 2).Id.Should().Be(second.Id);
            Subject.Find(_series.Id, 9, 9).Should().BeNull();
            Subject.Find(_series.Id, 2).Id.Should().Be(second.Id);
            Subject.Find(_series.Id, Past.AddDays(1).ToString(Episode.AIR_DATE_FORMAT)).Should().HaveCount(2);
            Subject.GetEpisodes(_series.Id).Should().HaveCount(3);
            Subject.GetEpisodes(_series.Id, 1).Should().HaveCount(3);
            Subject.GetEpisodes(_series.Id, 2).Should().BeEmpty();
            Subject.GetEpisodesBySeriesIds(new List<int> { _series.Id, _other.Id }).Should().HaveCount(4);
            Subject.GetEpisodeByFileId(file.Id).Should().HaveCount(2);
            Subject.GetEpisodesBySceneSeason(_series.Id, 11).Should().HaveCount(3);
            Subject.FindEpisodesBySceneNumbering(_series.Id, 11, 22).Select(e => e.Id).Should().Equal(second.Id);
            Subject.FindEpisodesBySceneNumbering(_series.Id, 102).Select(e => e.Id).Should().Equal(second.Id);
            first.Should().NotBeNull();
        }

        [Test]
        public void should_load_the_episode_file_of_an_episode()
        {
            var file = AddFile(1, "S01E01.mkv", Quality.HDTV720p);
            var episode = Add(1, 1, fileId: file.Id);
            Add(1, 2);

            Subject.Get(episode.Id).EpisodeFile.Value.RelativePath.Should().Be("S01E01.mkv");
            Subject.EpisodesWithFiles(_series.Id).Should().ContainSingle()
                   .Which.EpisodeFile.Value.Id.Should().Be(file.Id);
        }

        [Test]
        public void should_list_aired_episodes_without_files_and_skip_specials_unless_asked()
        {
            var file = AddFile(1, "S01E01.mkv", Quality.HDTV720p);
            Add(1, 1, fileId: file.Id);
            var missing = Add(1, 2);
            Add(1, 3, DateTime.UtcNow.AddDays(5));
            var special = Add(0, 1);

            var withoutSpecials = Subject.EpisodesWithoutFiles(Page(), false);
            var withSpecials = Subject.EpisodesWithoutFiles(Page(), true);

            withoutSpecials.Records.Select(e => e.Id).Should().Equal(missing.Id);
            withoutSpecials.TotalRecords.Should().Be(1);
            withSpecials.Records.Select(e => e.Id).Should().BeEquivalentTo(new[] { missing.Id, special.Id });
            withSpecials.TotalRecords.Should().Be(2);
            withoutSpecials.Records.Single().Series.Title.Should().Be("Breaking Bad");
        }

        [Test]
        public void should_only_list_missing_episodes_of_series_with_a_given_tag()
        {
            var missing = Add(1, 2);
            Add(1, 2, series: _other);

            _other.Tags = new HashSet<int> { 9 };
            _seriesRepository.Update(_other);

            Subject.EpisodesWithoutFiles(Page(), false, new HashSet<int> { 5 }).Records.Select(e => e.Id).Should().Equal(missing.Id);
            Subject.EpisodesWithoutFiles(Page(), false, new HashSet<int> { 9 }).Records.Should().ContainSingle().Which.SeriesId.Should().Be(_other.Id);
            Subject.EpisodesWithoutFiles(Page(), false, new HashSet<int> { 77 }).Records.Should().BeEmpty();
        }

        [Test]
        public void should_list_episodes_whose_file_is_below_the_cutoff()
        {
            var low = AddFile(1, "low.mkv", Quality.SDTV);
            var high = AddFile(1, "high.mkv", Quality.Bluray1080p);
            var below = Add(1, 1, fileId: low.Id);
            Add(1, 2, fileId: high.Id);
            Add(1, 3);

            var belowCutoff = new List<QualitiesBelowCutoff> { new QualitiesBelowCutoff(1, new[] { Quality.SDTV.Id, Quality.DVD.Id }) };

            var page = Subject.EpisodesWhereCutoffUnmet(Page(), belowCutoff, false);

            page.Records.Select(e => e.Id).Should().Equal(below.Id);
            page.TotalRecords.Should().Be(1);
        }

        [Test]
        public void should_not_list_cutoff_unmet_episodes_of_another_profile()
        {
            var low = AddFile(1, "low.mkv", Quality.SDTV);
            Add(1, 1, fileId: low.Id);

            var belowCutoff = new List<QualitiesBelowCutoff> { new QualitiesBelowCutoff(2, new[] { Quality.SDTV.Id }) };

            Subject.EpisodesWhereCutoffUnmet(Page(), belowCutoff, false).Records.Should().BeEmpty();
        }

        [Test]
        public void should_filter_cutoff_unmet_by_tag_and_quality()
        {
            var low = AddFile(1, "low.mkv", Quality.SDTV);
            var dvd = AddFile(1, "dvd.mkv", Quality.DVD);
            var sd = Add(1, 1, fileId: low.Id);
            var dvdEpisode = Add(1, 2, fileId: dvd.Id);
            var belowCutoff = new List<QualitiesBelowCutoff> { new QualitiesBelowCutoff(1, new[] { Quality.SDTV.Id, Quality.DVD.Id }) };

            Subject.EpisodesWhereCutoffUnmet(Page(), belowCutoff, false, new HashSet<int> { 3 }).Records.Should().HaveCount(2);
            Subject.EpisodesWhereCutoffUnmet(Page(), belowCutoff, false, new HashSet<int> { 99 }).Records.Should().BeEmpty();
            Subject.EpisodesWhereCutoffUnmet(Page(), belowCutoff, false, null, new List<int> { Quality.DVD.Id }).Records.Select(e => e.Id).Should().Equal(dvdEpisode.Id);
            sd.Should().NotBeNull();
        }

        [Test]
        public void should_list_episodes_between_dates_and_hide_unmonitored_when_asked()
        {
            var monitored = Add(1, 1, Past);
            Add(1, 2, Past.AddDays(1), false);
            Add(1, 3, Past.AddDays(40));
            Add(0, 1, Past);

            var unmonitoredSeries = TvTestData.NewSeries(5, "Unmonitored");
            unmonitoredSeries.Monitored = false;
            _seriesRepository.Insert(unmonitoredSeries);
            Add(1, 1, Past, true, 0, unmonitoredSeries);

            var from = Past.AddDays(-1);
            var to = Past.AddDays(2);

            Subject.EpisodesBetweenDates(from, to, true, true).Should().HaveCount(4);
            Subject.EpisodesBetweenDates(from, to, true, false).Should().HaveCount(3);
            Subject.EpisodesBetweenDates(from, to, false, false).Select(e => e.Id).Should().Equal(monitored.Id);
        }

        [Test]
        public void should_set_monitored_by_season_and_by_id()
        {
            var one = Add(1, 1, monitored: false);
            var two = Add(1, 2, monitored: false);
            var three = Add(2, 1, monitored: false);

            Subject.SetMonitoredBySeason(_series.Id, 1, true);

            Subject.Get(one.Id).Monitored.Should().BeTrue();
            Subject.Get(two.Id).Monitored.Should().BeTrue();
            Subject.Get(three.Id).Monitored.Should().BeFalse();

            Subject.SetMonitored(new[] { three.Id, one.Id }, false);

            Subject.Get(one.Id).Monitored.Should().BeFalse();
            Subject.Get(three.Id).Monitored.Should().BeFalse();
            Subject.Get(two.Id).Monitored.Should().BeTrue();

            Subject.SetMonitoredFlat(Subject.Get(three.Id), true);

            Subject.Get(three.Id).Monitored.Should().BeTrue();
        }

        [Test]
        public void should_monitor_by_type()
        {
            var file = AddFile(1, "S01E01.mkv", Quality.HDTV720p);
            var old = DateTime.UtcNow.AddDays(-200);
            var special = Add(0, 1, old, false);
            var haveFile = Add(1, 1, old, false, file.Id);
            var missing = Add(1, 2, old, false);
            var future = Add(2, 1, DateTime.UtcNow.AddDays(10), false);
            var recent = Add(2, 2, DateTime.UtcNow.AddDays(-10), false);

            List<int> Monitored() => Subject.GetEpisodes(_series.Id).Where(e => e.Monitored).Select(e => e.Id).OrderBy(x => x).ToList();
            List<int> Ids(params Episode[] episodes) => episodes.Select(e => e.Id).OrderBy(x => x).ToList();

            Subject.SetMonitored(_series.Id, MonitorTypes.All, 1, 2).Should().BeEquivalentTo(new[] { 1, 2 });
            Monitored().Should().Equal(Ids(haveFile, missing, future, recent));

            Subject.SetMonitored(_series.Id, MonitorTypes.None, 1, 2).Should().BeEmpty();
            Monitored().Should().BeEmpty();

            Subject.SetMonitored(_series.Id, MonitorTypes.Missing, 1, 2);
            Monitored().Should().Equal(Ids(missing, future, recent));

            Subject.SetMonitored(_series.Id, MonitorTypes.Existing, 1, 2);
            Monitored().Should().Equal(Ids(haveFile));

            Subject.SetMonitored(_series.Id, MonitorTypes.Future, 1, 2);
            Monitored().Should().Equal(Ids(future));

            Subject.SetMonitored(_series.Id, MonitorTypes.Recent, 1, 2);
            Monitored().Should().Equal(Ids(future, recent));

            Subject.SetMonitored(_series.Id, MonitorTypes.FirstSeason, 1, 2);
            Monitored().Should().Equal(Ids(haveFile, missing));

            Subject.SetMonitored(_series.Id, MonitorTypes.LastSeason, 1, 2);
            Monitored().Should().Equal(Ids(future, recent));

            Subject.SetMonitored(_series.Id, MonitorTypes.Pilot, 1, 2);
            Monitored().Should().Equal(Ids(haveFile));

            Subject.SetMonitored(_series.Id, MonitorTypes.MonitorSpecials, 1, 2);
            Monitored().Should().Contain(special.Id);

            Subject.SetMonitored(_series.Id, MonitorTypes.UnmonitorSpecials, 1, 2);
            Monitored().Should().NotContain(special.Id);
        }

        [Test]
        public void should_link_and_unlink_a_file_and_publish_the_change()
        {
            var file = AddFile(1, "S01E01.mkv", Quality.HDTV720p);
            var episode = Add(1, 1);

            Subject.SetFileId(episode, file.Id);

            Subject.Get(episode.Id).EpisodeFileId.Should().Be(file.Id);
            Subject.Get(episode.Id).HasFile.Should().BeTrue();

            Subject.ClearFileId(episode, true);

            var stored = Subject.Get(episode.Id);
            stored.EpisodeFileId.Should().Be(0);
            stored.Monitored.Should().BeFalse();

            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<ModelEvent<Episode>>(m => m.Action == ModelAction.Updated && m.Model.Id == episode.Id)),
                Times.Exactly(2));
        }

        [Test]
        public void should_not_publish_an_event_for_a_plain_update()
        {
            var episode = Add(1, 1);

            episode.Title = "Changed";
            Subject.Update(episode);

            Mocker.GetMock<IEventAggregator>().Verify(e => e.PublishEvent(It.IsAny<ModelEvent<Episode>>()), Times.Never());
        }
    }
}
