using System;
using System.Collections.Generic;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Music.Releases;

namespace NzbDrone.Api.Test.v3.Music
{
    [TestFixture]
    public class MusicReleaseResourceFixture : TestBase
    {
        private static DownloadDecision NewDecision(ReleaseInfo release, params DownloadRejection[] rejections)
        {
            var remote = new RemoteAlbum
            {
                Release = release,
                CustomFormats = new List<CustomFormat> { new CustomFormat("Lossless") { Id = 7 } },
                CustomFormatScore = 100,
                DownloadAllowed = true,
                ParsedAlbumInfo = new ParsedAlbumInfo
                {
                    ArtistName = "The Black Keys",
                    AlbumTitle = "Brothers",
                    ReleaseGroup = "GRP",
                    Discography = true,
                    Quality = new AudioQualityModel(AudioQuality.FLAC)
                }
            };

            return new DownloadDecision(remote, rejections);
        }

        [Test]
        public void should_map_a_usenet_release()
        {
            var release = new ReleaseInfo { Guid = "g", Title = "T", Size = 1234, IndexerId = 3, Indexer = "Idx", DownloadProtocol = DownloadProtocol.Usenet, DownloadUrl = "http://x/dl", PublishDate = DateTime.UtcNow.AddHours(-3) };

            var resource = NewDecision(release).ToResource();

            resource.Guid.Should().Be("g");
            resource.Size.Should().Be(1234);
            resource.Quality.Quality.Should().Be(AudioQuality.FLAC);
            resource.ArtistName.Should().Be("The Black Keys");
            resource.AlbumTitle.Should().Be("Brothers");
            resource.ReleaseGroup.Should().Be("GRP");
            resource.Discography.Should().BeTrue();
            resource.Protocol.Should().Be(DownloadProtocol.Usenet);
            resource.Approved.Should().BeTrue();
            resource.DownloadAllowed.Should().BeTrue();
            resource.CustomFormatScore.Should().Be(100);
            resource.CustomFormats.Should().ContainSingle(f => f.Name == "Lossless");
            resource.Seeders.Should().BeNull();
            resource.Leechers.Should().BeNull();
            resource.Age.Should().Be(0);
            resource.AgeHours.Should().BeGreaterThan(2);
        }

        [Test]
        public void should_map_a_torrent_release_with_seeders_leechers_and_flags()
        {
            var release = new TorrentInfo { Guid = "g", Title = "T", MagnetUrl = "magnet:?x", InfoHash = "abc", Seeders = 5, Peers = 8, DownloadProtocol = DownloadProtocol.Torrent };
            release.AddIndexerFlags(IndexerFlags.G_Freeleech);

            var resource = NewDecision(release).ToResource();

            resource.MagnetUrl.Should().Be("magnet:?x");
            resource.InfoHash.Should().Be("abc");
            resource.Seeders.Should().Be(5);
            resource.Leechers.Should().Be(3);
            ((IEnumerable<string>)resource.IndexerFlags).Should().Contain("G_Freeleech");
        }

        [Test]
        public void should_map_the_rejections_to_their_messages_and_flag_the_decision()
        {
            var decision = NewDecision(new ReleaseInfo(), new DownloadRejection(DownloadRejectionReason.MinimumAge, "Too young"), new DownloadRejection(DownloadRejectionReason.Blocklisted, "Blocklisted"));

            var resource = decision.ToResource();

            resource.Rejections.Should().Equal("Too young", "Blocklisted");
            resource.Rejected.Should().BeTrue();
            resource.Approved.Should().BeFalse();
        }

        [Test]
        public void should_read_a_torrent_back_into_a_model_with_numeric_flags()
        {
            var json = "{\"title\":\"T\",\"protocol\":\"torrent\",\"seeders\":4,\"leechers\":2,\"magnetUrl\":\"magnet:?x\",\"indexerFlags\":1,\"publishDate\":\"2026-10-01T00:00:00Z\"}";
            var resource = JsonSerializer.Deserialize<MusicReleaseResource>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } });

            var model = resource.ToModel();

            var torrent = model.Should().BeOfType<TorrentInfo>().Subject;
            torrent.Seeders.Should().Be(4);
            torrent.Peers.Should().Be(6);
            torrent.MagnetUrl.Should().Be("magnet:?x");
            torrent.ToIndexerFlags().Should().Be((IndexerFlags)1);
            model.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
        }

        [Test]
        public void should_read_a_usenet_release_back_into_a_plain_model()
        {
            var resource = new MusicReleaseResource { Guid = "g", Title = "T", Size = 9, Protocol = DownloadProtocol.Usenet, DownloadUrl = "http://x", IndexerId = 2, Indexer = "Idx", PublishDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc) };

            var model = resource.ToModel();

            model.Should().NotBeOfType<TorrentInfo>();
            model.Guid.Should().Be("g");
            model.Size.Should().Be(9);
            model.IndexerId.Should().Be(2);
            model.DownloadProtocol.Should().Be(DownloadProtocol.Usenet);
        }
    }
}
