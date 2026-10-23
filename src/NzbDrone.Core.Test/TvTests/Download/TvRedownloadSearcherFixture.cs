using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Downloads;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.Download
{
    [TestFixture]
    public class TvRedownloadSearcherFixture : CoreTest<TvRedownloadSearcher>
    {
        private static DownloadFailedEvent Failure(params int[] episodeIds)
        {
            return new DownloadFailedEvent { Kind = MediaKind.Tv, ParentId = 5, ItemIds = episodeIds.ToList() };
        }

        private void GivenSeason(int seasonNumber, int episodeCount)
        {
            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisode(It.IsAny<int>()))
                  .Returns(new Episode { SeasonNumber = seasonNumber });

            Mocker.GetMock<IEpisodeService>()
                  .Setup(s => s.GetEpisodesBySeason(5, seasonNumber))
                  .Returns(Enumerable.Range(1, episodeCount).Select(i => new Episode { Id = i }).ToList());
        }

        [Test]
        public void should_be_the_tv_searcher()
        {
            Subject.Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void should_not_search_when_the_failure_has_no_episodes()
        {
            Subject.Search(Failure());

            Mocker.GetMock<IManageCommandQueue>().Verify(c => c.Push(It.IsAny<Command>()), Times.Never());
        }

        [Test]
        public void should_search_again_for_a_single_episode()
        {
            Subject.Search(Failure(11));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<EpisodeSearchCommand>(e => e.EpisodeIds.SequenceEqual(new[] { 11 }))), Times.Once());
        }

        [Test]
        public void should_search_the_season_when_the_whole_season_failed()
        {
            GivenSeason(2, 3);

            Subject.Search(Failure(11, 12, 13));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<SeasonSearchCommand>(s => s.SeriesId == 5 && s.SeasonNumber == 2)), Times.Once());
        }

        [Test]
        public void should_search_the_episodes_when_part_of_a_season_failed()
        {
            GivenSeason(2, 10);

            Subject.Search(Failure(11, 12));

            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.Is<EpisodeSearchCommand>(e => e.EpisodeIds.SequenceEqual(new[] { 11, 12 }))), Times.Once());
            Mocker.GetMock<IManageCommandQueue>()
                  .Verify(c => c.Push(It.IsAny<SeasonSearchCommand>()), Times.Never());
        }
    }
}
