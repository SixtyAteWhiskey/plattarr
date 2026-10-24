using System;
using System.Collections.Generic;
using Dapper;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Housekeepers;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Test.MusicTests;
using MusicBlocklist = NzbDrone.Core.Music.Blocklisting.Blocklist;
using MusicHistory = NzbDrone.Core.Music.History.AlbumHistory;
using MusicHistoryEventType = NzbDrone.Core.Music.History.AlbumHistoryEventType;

namespace NzbDrone.Core.Test.Housekeeping.Housekeepers
{
    [TestFixture]
    public class CleanupOrphanedMusicActivityFixture : DbTest<CleanupOrphanedMusicActivity, MusicHistory>
    {
        private Artist _artist;
        private Album _album;

        [SetUp]
        public void Setup()
        {
            var metadata = MusicTestData.NewMetadata("A", "mb-1");
            metadata.OldForeignArtistIds = new List<string>();
            Mocker.Resolve<ArtistMetadataRepository>().Insert(metadata);

            _artist = MusicTestData.NewArtist(metadata.Id, name: "A");
            Mocker.Resolve<ArtistRepository>().Insert(_artist);

            _album = MusicTestData.NewAlbum(metadata.Id, "Album", "mb-a1");
            Mocker.Resolve<AlbumRepository>().Insert(_album);
        }

        private MusicHistory NewHistory(MediaKind kind, int parentId, int itemId)
        {
            return new MusicHistory
            {
                MediaKind = kind,
                EventType = MusicHistoryEventType.Grabbed,
                ParentId = parentId,
                ItemId = itemId,
                SourceTitle = "A - Album",
                Quality = new AudioQualityModel(),
                Date = DateTime.UtcNow
            };
        }

        private int Count(string table, MediaKind kind)
        {
            using var mapper = Mocker.Resolve<IMainDatabase>().OpenConnection();

            return mapper.ExecuteScalar<int>($@"SELECT COUNT(*) FROM ""{table}"" WHERE ""MediaKind"" = {(int)kind}");
        }

        [Test]
        public void should_delete_history_of_an_artist_that_is_gone()
        {
            Db.Insert(NewHistory(MediaKind.Music, _artist.Id + 100, _album.Id));

            Subject.Clean();

            Count("History", MediaKind.Music).Should().Be(0);
        }

        [Test]
        public void should_delete_history_of_an_album_that_is_gone()
        {
            Db.Insert(NewHistory(MediaKind.Music, _artist.Id, _album.Id + 100));

            Subject.Clean();

            Count("History", MediaKind.Music).Should().Be(0);
        }

        [Test]
        public void should_keep_history_whose_artist_and_album_exist()
        {
            Db.Insert(NewHistory(MediaKind.Music, _artist.Id, _album.Id));

            Subject.Clean();

            Count("History", MediaKind.Music).Should().Be(1);
        }

        [Test]
        public void should_not_touch_rows_of_other_media_types()
        {
            Db.Insert(NewHistory(MediaKind.Movies, 4242, 0));
            Db.Insert(NewHistory(MediaKind.Tv, 4242, 4242));

            Subject.Clean();

            Count("History", MediaKind.Movies).Should().Be(1);
            Count("History", MediaKind.Tv).Should().Be(1);
        }

        [Test]
        public void should_delete_blocklist_of_an_artist_that_is_gone_and_keep_the_rest()
        {
            Db.Insert(new MusicBlocklist
            {
                MediaKind = MediaKind.Music,
                ParentId = _artist.Id + 100,
                ItemIds = new List<int> { 1 },
                SourceTitle = "gone",
                Quality = new AudioQualityModel()
            });
            Db.Insert(new MusicBlocklist
            {
                MediaKind = MediaKind.Music,
                ParentId = _artist.Id,
                ItemIds = new List<int> { _album.Id },
                SourceTitle = "kept",
                Quality = new AudioQualityModel()
            });

            Subject.Clean();

            Count("Blocklist", MediaKind.Music).Should().Be(1);
        }
    }
}
