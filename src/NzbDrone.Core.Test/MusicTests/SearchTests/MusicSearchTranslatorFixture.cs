using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    // Each expectation is the request Lidarr's NewznabRequestGenerator sent for the same criteria,
    // written as the indexer layer's criteria.
    [TestFixture]
    public class MusicSearchTranslatorFixture : CoreTest<MusicSearchTranslator>
    {
        private Artist _artist;
        private IndexerDefinition _indexer;

        [SetUp]
        public void SetUp()
        {
            _artist = new Artist { Metadata = new ArtistMetadata { Name = "The Black Keys" } };
            _indexer = new IndexerDefinition { Id = 1, Capabilities = FullCapabilities() };
        }

        private static IndexerCapabilities FullCapabilities()
        {
            return new IndexerCapabilities
            {
                SearchParams = new List<SearchParam> { SearchParam.Q },
                MusicSearchParams = new List<MusicSearchParam> { MusicSearchParam.Q, MusicSearchParam.Artist, MusicSearchParam.Album }
            };
        }

        private List<IReadOnlyList<SearchCriteriaBase>> Translate(MusicSearchRequest request)
        {
            return Subject.Translate(request, _indexer, new[] { 3000 }).ToList();
        }

        private AlbumSearchRequest Album(string title = "El Camino", string disambiguation = null)
        {
            return new AlbumSearchRequest { Artist = _artist, AlbumTitle = title, Disambiguation = disambiguation };
        }

        [Test]
        public void album_search_should_search_music_with_artist_and_album_then_text()
        {
            var tiers = Translate(Album());

            tiers.Should().HaveCount(2);

            var music = tiers[0].Should().ContainSingle().Subject.Should().BeOfType<MusicSearchCriteria>().Subject;
            music.Artist.Should().Be("Black Keys");
            music.Album.Should().Be("El Camino");
            music.SearchType.Should().Be("music");
            music.Categories.Should().Equal(3000);
            music.Source.Should().Be(MusicSearchTranslator.Source);

            var text = tiers[1].Should().ContainSingle().Subject.Should().BeOfType<BasicSearchCriteria>().Subject;
            text.SearchTerm.Should().Be("Black Keys El Camino");
            text.SearchType.Should().Be("search");
            text.Categories.Should().Equal(3000);
        }

        [Test]
        public void artist_search_should_search_music_with_the_artist_only()
        {
            var tiers = Translate(new ArtistSearchRequest { Artist = _artist });

            var music = tiers[0].Single().Should().BeOfType<MusicSearchCriteria>().Subject;
            music.Artist.Should().Be("Black Keys");
            music.Album.Should().BeNull();

            tiers[1].Single().Should().BeOfType<BasicSearchCriteria>().Which.SearchTerm.Should().Be("Black Keys");
        }

        [Test]
        public void should_join_the_disambiguation_into_the_album()
        {
            var tiers = Translate(Album("Brothers", "Deluxe Edition"));

            tiers[0].Single().Should().BeOfType<MusicSearchCriteria>().Which.Album.Should().Be("Brothers Deluxe Edition");
            tiers[1].Single().Should().BeOfType<BasicSearchCriteria>().Which.SearchTerm.Should().Be("Black Keys Brothers Deluxe Edition");
        }

        [Test]
        public void should_leave_out_the_album_when_the_indexer_has_no_album_parameter()
        {
            _indexer.Capabilities.MusicSearchParams = new List<MusicSearchParam> { MusicSearchParam.Artist };

            var music = Translate(Album())[0].Single().Should().BeOfType<MusicSearchCriteria>().Subject;

            music.Artist.Should().Be("Black Keys");
            music.Album.Should().BeNull();
        }

        [Test]
        public void should_leave_out_the_artist_when_the_indexer_has_no_artist_parameter()
        {
            _indexer.Capabilities.MusicSearchParams = new List<MusicSearchParam> { MusicSearchParam.Album };

            var music = Translate(Album())[0].Single().Should().BeOfType<MusicSearchCriteria>().Subject;

            music.Artist.Should().BeNull();
            music.Album.Should().Be("El Camino");
        }

        [Test]
        public void should_skip_the_music_tier_when_neither_parameter_is_available()
        {
            _indexer.Capabilities.MusicSearchParams = new List<MusicSearchParam> { MusicSearchParam.Q };

            var tiers = Translate(Album());

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<BasicSearchCriteria>();
        }

        [Test]
        public void should_skip_the_music_tier_when_the_indexer_has_no_music_search()
        {
            _indexer.Capabilities.MusicSearchParams = new List<MusicSearchParam>();

            var tiers = Translate(Album());

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<BasicSearchCriteria>();
        }

        [Test]
        public void should_skip_the_text_tier_when_the_indexer_has_no_search()
        {
            _indexer.Capabilities.SearchParams = new List<SearchParam>();

            var tiers = Translate(Album());

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<MusicSearchCriteria>();
        }

        [Test]
        public void should_search_by_text_only_when_the_capabilities_are_not_known_yet()
        {
            _indexer.Capabilities = null;

            var tiers = Translate(Album());

            tiers.Should().ContainSingle();
            tiers[0].Single().Should().BeOfType<BasicSearchCriteria>().Which.SearchTerm.Should().Be("Black Keys El Camino");
        }

        [Test]
        public void should_search_for_various_artists_as_va()
        {
            _artist.Metadata.Value.Name = "Various Artists";

            Translate(Album("Now 99"))[1].Single().Should().BeOfType<BasicSearchCriteria>().Which.SearchTerm.Should().Be("VA Now 99");
        }
    }
}
