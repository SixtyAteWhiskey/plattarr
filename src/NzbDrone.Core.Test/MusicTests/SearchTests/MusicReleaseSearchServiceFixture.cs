using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using ReleaseSearchService = NzbDrone.Core.Music.Search.ReleaseSearchService;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    [TestFixture]
    public class MusicReleaseSearchServiceFixture : CoreTest<ReleaseSearchService>
    {
        private Artist _artist;
        private Album _monitored;
        private Album _unmonitored;
        private List<ReleaseInfo> _releases;
        private List<MusicSearchRequest> _requests;
        private int _indexersSearched;

        [SetUp]
        public void SetUp()
        {
            _artist = new Artist { Id = 1, Tags = new HashSet<int> { 3, 4 }, Metadata = new ArtistMetadata { Name = "Coldplay" } };
            _monitored = new Album { Id = 10, ArtistId = 1, Title = "Parachutes", Monitored = true, ReleaseDate = new DateTime(2000, 7, 10), Disambiguation = "UK" };
            _unmonitored = new Album { Id = 11, ArtistId = 1, Title = "X&Y", Monitored = false };
            _releases = new List<ReleaseInfo> { new ReleaseInfo { Title = "r" } };
            _requests = new List<MusicSearchRequest>();
            _indexersSearched = 2;

            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(1)).Returns(_artist);
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbum(10)).Returns(_monitored);
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbumsByArtist(1)).Returns(new List<Album> { _monitored, _unmonitored });

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Setup(f => f.Search(It.IsAny<MusicSearchRequest>(), It.IsAny<IEnumerable<int>>(), MediaKind.Music, It.IsAny<ISearchTranslator<MusicSearchRequest>>()))
                  .Callback<MusicSearchRequest, IEnumerable<int>, MediaKind, ISearchTranslator<MusicSearchRequest>>((request, _, _, _) => _requests.Add(request))
                  .ReturnsAsync(() => new IndexerSearchResult(_releases, _indexersSearched));

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<MusicSearchRequest>()))
                  .Returns(new List<DownloadDecision>());
        }

        [Test]
        public async Task album_search_should_search_the_album_with_its_year_and_disambiguation()
        {
            await Subject.AlbumSearch(10, false, true, false);

            var request = _requests.Should().ContainSingle().Subject.Should().BeOfType<AlbumSearchRequest>().Subject;

            request.Artist.Should().BeSameAs(_artist);
            request.Albums.Should().Equal(_monitored);
            request.AlbumTitle.Should().Be("Parachutes");
            request.AlbumYear.Should().Be(2000);
            request.Disambiguation.Should().Be("UK");
            request.UserInvokedSearch.Should().BeTrue();
            request.InteractiveSearch.Should().BeFalse();
        }

        [Test]
        public async Task album_search_should_not_set_a_year_for_an_album_without_a_release_date()
        {
            _monitored.ReleaseDate = null;

            await Subject.AlbumSearch(10, false, false, true);

            var request = _requests.Single().Should().BeOfType<AlbumSearchRequest>().Subject;

            request.AlbumYear.Should().Be(0);
            request.InteractiveSearch.Should().BeTrue();
        }

        [Test]
        public async Task artist_search_should_search_only_the_monitored_albums()
        {
            await Subject.ArtistSearch(1, false, false, false);

            var request = _requests.Single().Should().BeOfType<ArtistSearchRequest>().Subject;

            request.Artist.Should().BeSameAs(_artist);
            request.Albums.Should().Equal(_monitored);
        }

        [Test]
        public async Task should_pass_the_artist_tags_so_the_fetcher_can_filter_indexers()
        {
            await Subject.ArtistSearch(1, false, false, false);

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Verify(f => f.Search(It.IsAny<MusicSearchRequest>(), It.Is<IEnumerable<int>>(t => t.OrderBy(i => i).SequenceEqual(new[] { 3, 4 })), MediaKind.Music, It.IsAny<ISearchTranslator<MusicSearchRequest>>()), Times.Once());
        }

        [Test]
        public async Task should_search_with_the_music_translator()
        {
            await Subject.ArtistSearch(1, false, false, false);

            Mocker.GetMock<IIndexerReleaseFetcher>()
                  .Verify(f => f.Search(It.IsAny<MusicSearchRequest>(), It.IsAny<IEnumerable<int>>(), MediaKind.Music, Mocker.Resolve<ISearchTranslator<MusicSearchRequest>>()), Times.Once());
        }

        [Test]
        public async Task should_make_decisions_on_the_fetched_releases_for_the_request()
        {
            await Subject.AlbumSearch(10, false, false, false);

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Verify(s => s.GetSearchDecision(_releases, _requests.Single()), Times.Once());
        }

        [Test]
        public async Task should_set_the_last_search_time_when_an_indexer_was_searched()
        {
            await Subject.AlbumSearch(10, false, false, false);

            _monitored.LastSearchTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            Mocker.GetMock<IAlbumService>()
                  .Verify(s => s.UpdateLastSearchTime(It.Is<List<Album>>(a => a.Single() == _monitored)), Times.Once());
        }

        [Test]
        public async Task should_not_set_the_last_search_time_when_no_indexer_was_searched()
        {
            _indexersSearched = 0;

            await Subject.AlbumSearch(10, false, false, false);

            _monitored.LastSearchTime.Should().BeNull();

            Mocker.GetMock<IAlbumService>()
                  .Verify(s => s.UpdateLastSearchTime(It.IsAny<List<Album>>()), Times.Never());
        }

        [Test]
        public async Task should_return_one_decision_for_each_guid_with_the_fewest_rejections()
        {
            DownloadDecision Decision(string guid, int priority, params DownloadRejection[] rejections)
            {
                var remote = new RemoteAlbum { Release = new ReleaseInfo { Guid = guid, IndexerPriority = priority } };

                return new DownloadDecision(remote, rejections);
            }

            var rejection = new DownloadRejection(DownloadRejectionReason.Unknown, "no");

            var rejected = Decision("a", 25, rejection);
            var accepted = Decision("a", 25);
            var lowPriority = Decision("b", 50);
            var highPriority = Decision("b", 1);

            Mocker.GetMock<IMakeDownloadDecision>()
                  .Setup(s => s.GetSearchDecision(It.IsAny<List<ReleaseInfo>>(), It.IsAny<MusicSearchRequest>()))
                  .Returns(new List<DownloadDecision> { rejected, accepted, lowPriority, highPriority });

            var result = await Subject.ArtistSearch(1, false, false, false);

            result.Should().HaveCount(2);
            result.Should().Contain(accepted);
            result.Should().Contain(highPriority);
        }
    }
}
