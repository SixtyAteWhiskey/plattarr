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
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    [TestFixture]
    public class MusicAlbumSearchServiceFixture : CoreTest<AlbumSearchService>
    {
        private List<int> _searched;
        private List<Album> _missing;
        private List<Album> _cutoffUnmet;
        private ProcessedDecisions _processed;

        [SetUp]
        public void SetUp()
        {
            _searched = new List<int>();
            _missing = new List<Album>();
            _cutoffUnmet = new List<Album>();
            _processed = new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>());

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.AlbumSearch(It.IsAny<int>(), false, It.IsAny<bool>(), false))
                  .Callback<int, bool, bool, bool>((id, _, _, _) => _searched.Add(id))
                  .ReturnsAsync(new List<DownloadDecision>());

            Mocker.GetMock<IProcessDownloadDecisions>()
                  .Setup(s => s.ProcessDecisions(It.IsAny<List<DownloadDecision>>()))
                  .ReturnsAsync(_processed);

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                  .Returns(() => new PagingSpec<Album> { Records = _missing });

            Mocker.GetMock<IAlbumCutoffService>()
                  .Setup(s => s.AlbumsWhereCutoffUnmet(It.IsAny<PagingSpec<Album>>()))
                  .Returns(() => new PagingSpec<Album> { Records = _cutoffUnmet });

            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>());
        }

        private static Album Album(int id, int artistId, DateTime? lastSearchTime = null)
        {
            return new Album { Id = id, ArtistId = artistId, Monitored = true, LastSearchTime = lastSearchTime };
        }

        private void GivenQueued(params int[] albumIds)
        {
            Mocker.GetMock<IQueueService>()
                  .Setup(s => s.GetQueue())
                  .Returns(new List<Queue.Queue>
                  {
                      new Queue.Queue { RemoteRelease = new RemoteAlbum { Albums = albumIds.Select(i => new Album { Id = i }).ToList() } }
                  });
        }

        [Test]
        public void should_search_each_requested_album()
        {
            Subject.Execute(new AlbumSearchCommand { AlbumIds = new List<int> { 4, 5 } });

            _searched.Should().Equal(4, 5);
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(It.IsAny<List<DownloadDecision>>()), Times.Exactly(2));
        }

        [Test]
        public void should_mark_a_manual_album_search_as_user_invoked()
        {
            Subject.Execute(new AlbumSearchCommand { AlbumIds = new List<int> { 4 }, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.AlbumSearch(4, false, true, false), Times.Once());
        }

        [Test]
        public void missing_search_should_search_the_albums_without_files_oldest_search_first()
        {
            _missing.AddRange(new[] { Album(1, 9, DateTime.UtcNow.AddDays(-1)), Album(2, 9), Album(3, 9, DateTime.UtcNow.AddDays(-5)) });

            Subject.Execute(new MissingAlbumSearchCommand());

            _searched.Should().Equal(2, 3, 1);
        }

        [Test]
        public void missing_search_should_ask_for_monitored_albums_of_monitored_artists()
        {
            PagingSpec<Album> asked = null;

            Mocker.GetMock<IAlbumService>()
                  .Setup(s => s.AlbumsWithoutFiles(It.IsAny<PagingSpec<Album>>()))
                  .Callback<PagingSpec<Album>>(p => asked = p)
                  .Returns(new PagingSpec<Album> { Records = new List<Album>() });

            Subject.Execute(new MissingAlbumSearchCommand());

            asked.FilterExpressions.Should().ContainSingle();

            var filter = asked.FilterExpressions.Single().Compile();

            filter(new Album { Monitored = true, Artist = new Artist { Monitored = true } }).Should().BeTrue();
            filter(new Album { Monitored = false, Artist = new Artist { Monitored = true } }).Should().BeFalse();
            filter(new Album { Monitored = true, Artist = new Artist { Monitored = false } }).Should().BeFalse();
        }

        [Test]
        public void missing_search_should_search_only_the_requested_artist()
        {
            _missing.AddRange(new[] { Album(1, 9), Album(2, 8), Album(3, 9) });

            Subject.Execute(new MissingAlbumSearchCommand { ArtistId = 9 });

            _searched.Should().BeEquivalentTo(new[] { 1, 3 });
        }

        [Test]
        public void missing_search_should_skip_albums_that_are_in_the_queue()
        {
            _missing.AddRange(new[] { Album(1, 9), Album(2, 9) });
            GivenQueued(1);

            Subject.Execute(new MissingAlbumSearchCommand());

            _searched.Should().Equal(2);
        }

        [Test]
        public void missing_search_should_carry_on_when_one_album_fails()
        {
            _missing.AddRange(new[] { Album(1, 9), Album(2, 9) });

            Mocker.GetMock<ISearchForReleases>()
                  .Setup(s => s.AlbumSearch(1, false, It.IsAny<bool>(), false))
                  .ThrowsAsync(new InvalidOperationException("indexer down"));

            Subject.Execute(new MissingAlbumSearchCommand());

            _searched.Should().Equal(2);
        }

        [Test]
        public void cutoff_search_should_search_the_albums_whose_cutoff_is_unmet()
        {
            _cutoffUnmet.AddRange(new[] { Album(1, 9), Album(2, 8) });

            Subject.Execute(new CutoffUnmetAlbumSearchCommand());

            _searched.Should().BeEquivalentTo(new[] { 1, 2 });
        }

        [Test]
        public void cutoff_search_should_search_only_the_requested_artist()
        {
            _cutoffUnmet.AddRange(new[] { Album(1, 9), Album(2, 8) });

            Subject.Execute(new CutoffUnmetAlbumSearchCommand { ArtistId = 8 });

            _searched.Should().Equal(2);
        }

        [Test]
        public void cutoff_search_should_skip_albums_that_are_in_the_queue()
        {
            _cutoffUnmet.AddRange(new[] { Album(1, 9), Album(2, 9) });
            GivenQueued(2);

            Subject.Execute(new CutoffUnmetAlbumSearchCommand());

            _searched.Should().Equal(1);
        }
    }
}
