using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using MovieBlocklist = NzbDrone.Core.Movies.Blocklisting.Blocklist;
using MovieBlocklistRepository = NzbDrone.Core.Movies.Blocklisting.BlocklistRepository;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicBlocklistRepositoryFixture : DbTest<BlocklistRepository, Blocklist>
    {
        private Artist _artist;
        private Artist _other;

        [SetUp]
        public void Setup()
        {
            _artist = AddArtist("The Black Keys", "artist-1");
            _other = AddArtist("Alien Ant Farm", "artist-2");
        }

        private Artist AddArtist(string name, string foreignId)
        {
            var metadata = MusicTestData.NewMetadata(name, foreignId);
            metadata.OldForeignArtistIds = new List<string>();
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            var artist = MusicTestData.NewArtist(metadata.Id, name: name);
            artist.Path = "/data/music/" + foreignId;
            Mocker.Resolve<ArtistRepository>().Insert(artist);

            return artist;
        }

        private static Blocklist NewBlocklist(Artist artist, string title = "Artist-Album-2010-FLAC-GRP", string hash = null)
        {
            return new Blocklist
            {
                MediaKind = MediaKind.Music,
                ParentId = artist.Id,
                ItemIds = new List<int> { 11, 12 },
                SourceTitle = title,
                Quality = new AudioQualityModel(AudioQuality.FLAC),
                Date = DateTime.UtcNow,
                PublishedDate = DateTime.UtcNow.AddDays(-1),
                Size = 987654321,
                Protocol = DownloadProtocol.Torrent,
                Indexer = "idx",
                IndexerFlags = IndexerFlags.G_Freeleech,
                Message = "failed",
                TorrentInfoHash = hash
            };
        }

        [Test]
        public void should_round_trip_item_ids_and_the_shared_columns()
        {
            var blocklist = NewBlocklist(_artist, hash: "ABC123");

            Subject.Insert(blocklist);

            var stored = Subject.Get(blocklist.Id);

            stored.Should().BeEquivalentTo(
                blocklist,
                o => o.Excluding(b => b.Artist)
                      .Using<DateTime?>(c => c.Subject.Should().BeCloseTo(c.Expectation.Value, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime?>()
                      .Using<DateTime>(c => c.Subject.Should().BeCloseTo(c.Expectation, TimeSpan.FromSeconds(1)))
                      .WhenTypeIs<DateTime>());
            stored.ItemIds.Should().Equal(11, 12);
            stored.Quality.Quality.Should().Be(AudioQuality.FLAC);
        }

        [Test]
        public void should_refuse_a_row_of_another_media_kind()
        {
            var blocklist = NewBlocklist(_artist);
            blocklist.MediaKind = MediaKind.Movies;

            var action = () => Subject.Insert(blocklist);

            action.Should().Throw<InvalidOperationException>();
        }

        [Test]
        public void should_query_by_artist_title_and_hash_case_insensitively()
        {
            Subject.Insert(NewBlocklist(_artist, "Artist-Album-2010-FLAC-GRP", "ABC123"));
            Subject.Insert(NewBlocklist(_other, "Artist-Album-2010-FLAC-GRP", "ABC123"));

            Subject.BlocklistedByTitle(_artist.Id, "ARTIST-ALBUM").Should().ContainSingle().Which.ParentId.Should().Be(_artist.Id);
            Subject.BlocklistedByTitle(_artist.Id, "other").Should().BeEmpty();
            Subject.BlocklistedByTorrentInfoHash(_artist.Id, "abc123").Should().ContainSingle().Which.ParentId.Should().Be(_artist.Id);
            Subject.BlocklistedByArtist(_other.Id).Should().ContainSingle();
        }

        [Test]
        public void should_not_see_or_delete_movie_rows_that_share_a_parent_id()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                SourceTitle = "Artist-Album-2010-FLAC-GRP",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_artist));

            Subject.All().Should().ContainSingle();
            Subject.Count().Should().Be(1);
            Subject.BlocklistedByTitle(_artist.Id, "Artist-Album").Should().ContainSingle();
            Subject.BlocklistedByArtist(_artist.Id).Should().ContainSingle();

            Subject.DeleteForArtistIds(new List<int> { _artist.Id });

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }

        [Test]
        public void should_purge_music_rows_and_leave_movie_rows()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                SourceTitle = "Movie",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_artist));
            Subject.Insert(NewBlocklist(_other));

            Subject.Purge();

            Subject.All().Should().BeEmpty();
            movieRepository.All().Should().ContainSingle();
        }

        [Test]
        public void should_page_only_music_rows_and_join_the_artist()
        {
            var movieRepository = Mocker.Resolve<MovieBlocklistRepository>();

            movieRepository.Insert(new MovieBlocklist
            {
                MediaKind = MediaKind.Movies,
                ParentId = _artist.Id,
                SourceTitle = "Movie",
                Quality = new QualityModel(),
                Date = DateTime.UtcNow,
                Languages = new List<Language>()
            });
            Subject.Insert(NewBlocklist(_artist));
            Subject.Insert(NewBlocklist(_other));

            var page = Subject.GetPaged(new PagingSpec<Blocklist> { Page = 1, PageSize = 10, SortKey = "date", SortDirection = SortDirection.Descending });

            page.TotalRecords.Should().Be(2);
            page.Records.Select(b => b.Artist.Name).Should().BeEquivalentTo(new[] { "The Black Keys", "Alien Ant Farm" });
        }
    }
}
