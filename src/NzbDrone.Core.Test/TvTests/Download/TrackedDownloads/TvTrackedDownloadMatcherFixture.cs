using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.Downloads;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.Download.TrackedDownloads
{
    [TestFixture]
    public class TvTrackedDownloadMatcherFixture : CoreTest<TvTrackedDownloadMatcher>
    {
        private void GivenDownloadHistory()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns([
                    new EpisodeHistory()
                    {
                        DownloadId = "35238",
                        SourceTitle = "TV Series S01",
                        ParentId = 5,
                        ItemId = 4
                    }
                ]);
        }

        private TrackedDownload Track(DownloadClientDefinition client, DownloadClientItem item, DownloadHistory downloadHistory = null)
        {
            var trackedDownload = new TrackedDownload { DownloadClient = client.Id, DownloadItem = item, Kind = MediaKind.Tv };

            Subject.Match(trackedDownload, downloadHistory);

            return trackedDownload;
        }

        [Test]
        public void should_track_downloads_using_the_source_title_if_it_cannot_be_found_using_the_download_title()
        {
            GivenDownloadHistory();

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = [new Episode { Id = 4 }],
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumbers = [1]
                },
                MappedSeasonNumber = 1
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(i => i.SeasonNumbers.Contains(1) && i.SeriesTitle == "TV Series"), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(remoteEpisode);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "The torrent release folder",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Track(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Id.Should().Be(5);
            trackedDownload.GetRemoteEpisode().Episodes.First().Id.Should().Be(4);
            trackedDownload.GetRemoteEpisode().ParsedEpisodeInfo.SeasonNumbers.Should().Equal(1);
            trackedDownload.GetRemoteEpisode().MappedSeasonNumber.Should().Be(1);
        }

        [Test]
        public void should_set_indexer()
        {
            var episodeHistory = new EpisodeHistory()
            {
                DownloadId = "35238",
                SourceTitle = "TV Series S01",
                ParentId = 5,
                ItemId = 4,
                EventType = EpisodeHistoryEventType.Grabbed,
            };
            episodeHistory.Data.Add("indexer", "MyIndexer (Prowlarr)");
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns([
                    episodeHistory
                ]);

            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer (Prowlarr)"
            };
            Mocker.GetMock<IIndexerOverrideService>()
                .Setup(v => v.Get(indexerDefinition.Id, MediaKind.Tv))
                .Returns(new IndexerOverride { IndexerId = indexerDefinition.Id, MediaKind = MediaKind.Tv, MultiLanguages = new List<int> { Language.Original.Id, Language.French.Id } });
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Get(indexerDefinition.Id))
                .Returns(indexerDefinition);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns([indexerDefinition]);

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = [new Episode { Id = 4 }],
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumbers = [1]
                },
                MappedSeasonNumber = 1
            };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                .Returns(remoteEpisode);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "TV.Series.S01.MULTi.1080p.WEB.H265-RlsGroup",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Track(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Release.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Release.Indexer.Should().Be("MyIndexer (Prowlarr)");
        }

        [Test]
        public void should_parse_as_special_when_source_title_parsing_fails()
        {
            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series() { Id = 5 },
                Episodes = [new Episode { Id = 4 }],
                ParsedEpisodeInfo = new ParsedEpisodeInfo()
                {
                    SeriesTitle = "TV Series",
                    SeasonNumbers = [0],
                    EpisodeNumbers = [1]
                },
                MappedSeasonNumber = 0
            };

            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns([
                    new EpisodeHistory()
                    {
                        DownloadId = "35238",
                        SourceTitle = "TV Series Special",
                        ParentId = 5,
                        ItemId = 4
                    }
                ]);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(i => i.SeasonNumbers.Contains(0) && i.SeriesTitle == "TV Series"), It.IsAny<int>(), It.IsAny<IEnumerable<int>>()))
                  .Returns(remoteEpisode);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.ParseSpecialEpisodeTitle(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                  .Returns(remoteEpisode.ParsedEpisodeInfo);

            var client = new DownloadClientDefinition()
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem()
            {
                Title = "The torrent release folder",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Track(client, item);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Id.Should().Be(5);
            trackedDownload.GetRemoteEpisode().Episodes.First().Id.Should().Be(4);
            trackedDownload.GetRemoteEpisode().ParsedEpisodeInfo.SeasonNumbers.Should().Equal(0);
            trackedDownload.GetRemoteEpisode().MappedSeasonNumber.Should().Be(0);
        }

        [Test]
        public void should_track_downloads_using_the_series_id_for_already_imported_downloads()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.Is<string>(sr => sr == "35238")))
                .Returns([]);

            Mocker.GetMock<ISeriesService>()
                .Setup(s => s.GetSeries(5))
                .Returns(new Series { Id = 5 });

            var downloadHistory = new DownloadHistory
            {
                ParentId = 5,
                EventType = DownloadHistoryEventType.DownloadImported,
            };

            var remoteEpisode = new RemoteEpisode
            {
                Series = new Series { Id = 5 },
                Episodes = [new Episode { Id = 4 }],
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    SeriesTitle = "TV Series",
                    SeasonNumbers = [1]
                },
                MappedSeasonNumber = 1
            };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.Is<ParsedEpisodeInfo>(i => i.SeasonNumbers.Contains(1) && i.SeriesTitle == "TV Series"), It.Is<Series>(x => x != null && x.Id == 5)))
                .Returns(remoteEpisode);

            var client = new DownloadClientDefinition
            {
                Id = 1,
                Protocol = DownloadProtocol.Torrent
            };

            var item = new DownloadClientItem
            {
                Title = "TV Series - S01E01",
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo
                {
                    Protocol = client.Protocol,
                    Id = client.Id,
                    Name = client.Name
                }
            };

            var trackedDownload = Track(client, item, downloadHistory);

            trackedDownload.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Should().NotBeNull();
            trackedDownload.GetRemoteEpisode().Series.Id.Should().Be(5);
            trackedDownload.GetRemoteEpisode().Episodes.First().Id.Should().Be(4);
            trackedDownload.GetRemoteEpisode().ParsedEpisodeInfo.SeasonNumbers.Should().Equal(1);
            trackedDownload.GetRemoteEpisode().MappedSeasonNumber.Should().Be(1);
        }

        private static DownloadClientItem Item(string title)
        {
            return new DownloadClientItem
            {
                Title = title,
                DownloadId = "35238",
                DownloadClientInfo = new DownloadClientItemClientInfo { Protocol = DownloadProtocol.Torrent, Id = 1, Name = "Client" }
            };
        }

        [Test]
        public void should_match_a_title_that_parses_as_an_episode_release()
        {
            Subject.MatchesTitle("TV.Series.S01E01.1080p.WEB.H264-RlsGroup").Should().BeTrue();
        }

        [Test]
        public void should_not_match_a_title_that_does_not_parse_as_an_episode_release()
        {
            Subject.MatchesTitle("A Movie (1998) 1080p BluRay").Should().BeFalse();
        }

        [Test]
        public void should_warn_when_the_release_matches_more_than_one_series()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                .Throws(new MultipleSeriesFoundException(new List<Series> { new Series { Title = "Show A" }, new Series { Title = "Show B" } }, "Multiple"));

            var trackedDownload = Track(new DownloadClientDefinition { Id = 1 }, Item("TV.Series.S01E01.1080p.WEB.H264-RlsGroup"));

            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            trackedDownload.StatusMessages.Single().Messages.Should().ContainSingle().Which.Should().StartWith("Unable to import automatically, found multiple series:").And.Contain("Show A").And.Contain("Show B");
            trackedDownload.GetRemoteEpisode().Should().BeNull();
        }

        [Test]
        public void should_warn_and_keep_tracking_when_matching_throws()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                .Throws(new System.InvalidOperationException("boom"));

            var trackedDownload = Track(new DownloadClientDefinition { Id = 1 }, Item("TV.Series.S01E01.1080p.WEB.H264-RlsGroup"));

            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Warning);
            trackedDownload.StatusMessages.Single().Messages.Should().ContainSingle().Which.Should().Be("Unable to parse episodes from title");
        }

        [Test]
        public void should_track_a_download_that_matches_no_series_without_a_warning()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(It.IsAny<string>()))
                .Returns(new List<EpisodeHistory>());

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                .Returns((RemoteEpisode)null);

            var trackedDownload = Track(new DownloadClientDefinition { Id = 1 }, Item("TV.Series.S01E01.1080p.WEB.H264-RlsGroup"));

            trackedDownload.GetRemoteEpisode().Should().BeNull();
            trackedDownload.Status.Should().Be(TrackedDownloadStatus.Ok);
        }

        [Test]
        public void rematch_should_map_the_title_again_and_replace_the_match()
        {
            var remoteEpisode = new RemoteEpisode { Series = new Series { Id = 9 }, Episodes = new List<Episode> { new Episode { Id = 90 } } };

            Mocker.GetMock<IParsingService>()
                .Setup(s => s.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), null))
                .Returns(remoteEpisode);

            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Tv,
                DownloadItem = Item("TV.Series.S01E01.1080p.WEB.H264-RlsGroup"),
                RemoteRelease = new RemoteEpisode { Series = new Series { Id = 1 } }
            };

            Subject.Rematch(trackedDownload);

            trackedDownload.GetRemoteEpisode().Should().BeSameAs(remoteEpisode);
            Mocker.GetMock<IRemoteEpisodeAggregationService>().Verify(a => a.Augment(remoteEpisode), Times.Once());
        }

        [Test]
        public void rematch_should_clear_the_match_when_the_title_no_longer_parses()
        {
            var trackedDownload = new TrackedDownload
            {
                Kind = MediaKind.Tv,
                DownloadItem = Item("not a release title"),
                RemoteRelease = new RemoteEpisode { Series = new Series { Id = 1 } }
            };

            Subject.Rematch(trackedDownload);

            trackedDownload.RemoteRelease.Should().BeNull();
        }
    }
}
