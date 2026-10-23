using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.SearchTests
{
    [TestFixture]
    public class TvEpisodeSearchServiceFixture : CoreTest<EpisodeSearchService>
    {
        private List<DownloadDecision> _decisions;

        [SetUp]
        public void Setup()
        {
            _decisions = new List<DownloadDecision>();

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .ReturnsAsync(_decisions);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .ReturnsAsync(_decisions);

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<List<Episode>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .ReturnsAsync(_decisions);

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>());
        }

        private static Episode GivenEpisode(int id, int seriesId, int seasonNumber, DateTime? lastSearch = null, bool monitored = true, int fileId = 0, DateTime? airDate = null)
        {
            return new Episode
            {
                Id = id,
                SeriesId = seriesId,
                SeasonNumber = seasonNumber,
                LastSearchTime = lastSearch,
                Monitored = monitored,
                EpisodeFileId = fileId,
                AirDateUtc = airDate ?? DateTime.UtcNow.AddDays(-10)
            };
        }

        private void GivenMissingEpisodes(params Episode[] episodes)
        {
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodeBySeries(It.IsAny<int>()))
                  .Returns(episodes.ToList());
        }

        private void GivenQueued(params int[] episodeIds)
        {
            var queue = new Queue.Queue
            {
                RemoteRelease = new RemoteEpisode { Episodes = episodeIds.Select(i => new Episode { Id = i }).ToList() }
            };

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue> { queue });
        }

        [Test]
        public void episode_search_should_search_each_episode_id()
        {
            Subject.Execute(new EpisodeSearchCommand(new List<int> { 3, 4 }) { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(3, true, false), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(4, true, false), Times.Once());
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(_decisions), Times.Exactly(2));
        }

        [Test]
        public void episode_search_should_not_be_user_invoked_when_not_manual()
        {
            Subject.Execute(new EpisodeSearchCommand(new List<int> { 3 }) { Trigger = CommandTrigger.Unspecified });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(3, false, false), Times.Once());
        }

        [Test]
        public void missing_search_should_only_search_monitored_aired_episodes_without_files()
        {
            GivenMissingEpisodes(
                GivenEpisode(1, 5, 1),
                GivenEpisode(2, 5, 2, monitored: false),
                GivenEpisode(3, 5, 3, fileId: 9),
                GivenEpisode(4, 5, 4, airDate: DateTime.UtcNow.AddDays(3)));

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.IsAny<Episode>(), false, false), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), false, false), Times.Once());
        }

        [Test]
        public void missing_search_should_search_unmonitored_episodes_when_asked()
        {
            GivenMissingEpisodes(
                GivenEpisode(1, 5, 1),
                GivenEpisode(2, 5, 2, monitored: false));

            Subject.Execute(new MissingEpisodeSearchCommand(5) { Monitored = false });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), false, false), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void missing_search_should_skip_queued_episodes()
        {
            GivenMissingEpisodes(GivenEpisode(1, 5, 1), GivenEpisode(2, 5, 2));
            GivenQueued(1);

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), false, false), Times.Once());
        }

        [Test]
        public void missing_search_should_ignore_queue_items_that_are_not_episodes()
        {
            GivenMissingEpisodes(GivenEpisode(1, 5, 1));

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue> { new Queue.Queue { RemoteRelease = null } });

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), false, false), Times.Once());
        }

        [Test]
        public void missing_search_should_search_a_season_when_several_episodes_of_it_are_missing()
        {
            GivenMissingEpisodes(
                GivenEpisode(1, 5, 1),
                GivenEpisode(2, 5, 1),
                GivenEpisode(3, 5, 2));

            Subject.Execute(new MissingEpisodeSearchCommand(5) { Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.SeasonSearch(5, 1, It.Is<List<Episode>>(l => l.Select(e => e.Id).SequenceEqual(new[] { 1, 2 })), true, true, false), Times.Once());

            Mocker.GetMock<ISearchForReleases>()
                  .Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 3), true, false), Times.Once());
        }

        [Test]
        public void missing_search_should_search_least_recently_searched_group_first()
        {
            GivenMissingEpisodes(
                GivenEpisode(1, 5, 1, lastSearch: DateTime.UtcNow.AddDays(-1)),
                GivenEpisode(2, 5, 2, lastSearch: DateTime.UtcNow.AddDays(-9)),
                GivenEpisode(3, 5, 3));

            var order = new List<int>();

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.IsAny<Episode>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .Callback<Episode, bool, bool>((e, _, _) => order.Add(e.Id))
                  .ReturnsAsync(_decisions);

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            order.Should().Equal(3, 2, 1);
        }

        [Test]
        public void missing_search_should_continue_when_an_episode_search_throws()
        {
            GivenMissingEpisodes(GivenEpisode(1, 5, 1), GivenEpisode(2, 5, 2));

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), It.IsAny<bool>(), It.IsAny<bool>()))
                  .ThrowsAsync(new InvalidOperationException("boom"));

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), false, false), Times.Once());
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(It.IsAny<List<DownloadDecision>>()), Times.Once());
        }

        [Test]
        public void missing_search_should_continue_when_a_season_search_throws()
        {
            GivenMissingEpisodes(GivenEpisode(1, 5, 1), GivenEpisode(2, 5, 1), GivenEpisode(3, 5, 2));

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(5, 1, It.IsAny<List<Episode>>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
                  .ThrowsAsync(new InvalidOperationException("boom"));

            Subject.Execute(new MissingEpisodeSearchCommand(5));

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 3), false, false), Times.Once());
        }

        [Test]
        public void missing_search_without_series_should_page_episodes_without_files_with_tags()
        {
            var tags = new HashSet<int> { 7 };

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, tags))
                  .Returns(new PagingSpec<Episode> { Records = new List<Episode> { GivenEpisode(1, 5, 1) } });

            Subject.Execute(new MissingEpisodeSearchCommand { SeriesTags = tags });

            Mocker.GetMock<IEpisodeService>().Verify(v => v.EpisodesWithoutFiles(It.IsAny<PagingSpec<Episode>>(), true, tags), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), false, false), Times.Once());
        }

        [Test]
        public void cutoff_unmet_search_should_search_unmet_episodes_and_skip_queued()
        {
            var tags = new HashSet<int> { 7 };
            var quality = new List<int> { 4 };

            Mocker.GetMock<IEpisodeCutoffService>()
                  .Setup(s => s.EpisodesWhereCutoffUnmet(It.IsAny<PagingSpec<Episode>>(), tags, quality))
                  .Returns(new PagingSpec<Episode> { Records = new List<Episode> { GivenEpisode(1, 5, 1), GivenEpisode(2, 5, 2) } });

            GivenQueued(2);

            Subject.Execute(new CutoffUnmetEpisodeSearchCommand { SeriesTags = tags, Quality = quality, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 1), true, false), Times.Once());
            Mocker.GetMock<ISearchForReleases>().Verify(v => v.EpisodeSearch(It.Is<Episode>(e => e.Id == 2), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());
        }
    }

    [TestFixture]
    public class TvSeasonSearchServiceFixture : CoreTest<SeasonSearchService>
    {
        [Test]
        public void should_search_the_season_monitored_only_and_process_the_decisions()
        {
            var decisions = new List<DownloadDecision>();

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(5, 2, false, true, true, false))
                  .ReturnsAsync(decisions);

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(decisions))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));

            Subject.Execute(new SeasonSearchCommand { SeriesId = 5, SeasonNumber = 2, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.SeasonSearch(5, 2, false, true, true, false), Times.Once());
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(decisions), Times.Once());
        }

        [Test]
        public void should_not_be_user_invoked_when_not_manual()
        {
            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.SeasonSearch(5, 2, false, true, false, false))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));

            Subject.Execute(new SeasonSearchCommand { SeriesId = 5, SeasonNumber = 2, Trigger = CommandTrigger.Unspecified });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.SeasonSearch(5, 2, false, true, false, false), Times.Once());
        }
    }
}
