using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Notifications;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class MusicActivityRendererFixture : CoreTest<MusicActivityRenderer>
    {
        private Artist _artist;
        private List<Album> _albums;

        [SetUp]
        public void Setup()
        {
            _artist = MusicGoldenData.Artist();
            _albums = new List<Album> { MusicGoldenData.Album(21, "Brothers"), MusicGoldenData.Album(22, "El Camino", "a1b2c3d4-0002") };
        }

        [Test]
        public void should_be_the_music_renderer()
        {
            Subject.Kind.Should().Be(MediaKind.Music);
        }

        [Test]
        public void should_return_null_for_a_missing_artist()
        {
            Subject.Describe((Artist)null, _albums).Should().BeNull();
        }

        [Test]
        public void should_describe_the_artist_as_a_subject_with_the_album_titles_as_its_item_label()
        {
            var subject = Subject.Describe(_artist, _albums);

            subject.Kind.Should().Be(MediaKind.Music);
            subject.KindLabel.Should().Be("Artist");
            subject.ItemKindLabel.Should().Be("Album");
            subject.ItemLabel.Should().Be("Brothers + El Camino");
            subject.OmitYearFromTitle.Should().BeTrue();
            subject.ParentId.Should().Be(1);
            subject.ParentTitle.Should().Be("The Black Keys");
            subject.ParentPath.Should().Be(_artist.Path);
            subject.Overview.Should().Be("An American rock duo.");
            subject.Genres.Should().Equal("Rock", "Blues");
            subject.Rating.Should().Be(8.5m);
            subject.Tags.Should().BeEquivalentTo(new[] { 1, 2 });
            subject.PosterUrl.Should().Be("https://img.example/poster.jpg");
            subject.FanartUrl.Should().Be("https://img.example/fanart.jpg");
        }

        [Test]
        public void should_link_the_title_to_musicbrainz_with_an_untyped_link()
        {
            var subject = Subject.Describe(_artist, _albums);

            subject.GetExternalId(ExternalIdTypes.MusicBrainz).Should().Be("d15721d8-56b4-453d-b506-fc915b14cba2");
            subject.MetadataUrl.Should().Be("https://musicbrainz.org/artist/d15721d8-56b4-453d-b506-fc915b14cba2");
            subject.Links.Should().ContainSingle().Which.Label.Should().Be("MusicBrainz");
        }

        [Test]
        public void should_skip_the_link_for_an_artist_without_a_musicbrainz_id()
        {
            _artist.Metadata.Value.ForeignArtistId = null;

            var subject = Subject.Describe(_artist, _albums);

            subject.Links.Should().BeEmpty();
            subject.MetadataUrl.Should().BeNull();
        }

        [Test]
        public void should_have_an_empty_item_label_without_albums()
        {
            Subject.Describe(_artist).ItemLabel.Should().BeEmpty();
            Subject.Describe(_artist, new List<Album>()).ItemLabel.Should().BeEmpty();
        }

        [Test]
        public void should_resolve_the_artist_and_albums_by_id()
        {
            Mocker.GetMock<IArtistService>().Setup(s => s.GetArtist(1)).Returns(_artist);
            Mocker.GetMock<IAlbumService>().Setup(s => s.GetAlbums(It.IsAny<IEnumerable<int>>())).Returns(_albums);

            Subject.Describe(1, new[] { 21, 22 }).ItemLabel.Should().Be("Brothers + El Camino");
            Subject.Describe(1, System.Array.Empty<int>()).ItemLabel.Should().BeEmpty();
        }

        [Test]
        public void should_resolve_the_root_folder_lazily_from_the_artist_path()
        {
            Mocker.GetMock<IRootFolderService>().Setup(s => s.GetBestRootFolderPath("/music/The Black Keys")).Returns("/music");

            var subject = Subject.Describe(_artist, _albums);

            Mocker.GetMock<IRootFolderService>().Verify(s => s.GetBestRootFolderPath(It.IsAny<string>()), Times.Never());
            subject.RootFolderPath.Should().Be("/music");
        }

        [Test]
        public void should_describe_a_track_file_relative_to_the_artist_folder()
        {
            var file = MusicGoldenData.TrackFile(_artist, _albums[0], 51, "01 - Everlasting Light.flac", AudioQuality.FLAC);

            var described = Subject.DescribeFile(_artist, file);

            described.Id.Should().Be(51);
            described.RelativePath.Should().Be("Brothers/01 - Everlasting Light.flac");
            described.QualityName.Should().Be("FLAC");
            described.ReleaseGroup.Should().Be("GRP");
            described.SceneName.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
            described.MediaInfo.AudioCodec.Should().Be("FLAC");
            described.MediaInfo.AudioChannels.Should().Be(2);
            described.MediaInfo.VideoCodec.Should().BeNull();
            Subject.DescribeFile(_artist, null).Should().BeNull();
        }

        [Test]
        public void should_describe_a_release()
        {
            var release = Subject.DescribeRelease(MusicGoldenData.Remote(_artist, _albums));

            release.Title.Should().Be("The.Black.Keys-Brothers-2010-FLAC-GRP");
            release.Indexer.Should().Be("Knaben");
            release.Size.Should().Be(314572800);
            release.QualityName.Should().Be("FLAC");
            release.ReleaseGroup.Should().Be("GRP");
            release.CustomFormats.Should().Equal("Lossless", "Vinyl");
            release.CustomFormatScore.Should().Be(150);
        }
    }
}
