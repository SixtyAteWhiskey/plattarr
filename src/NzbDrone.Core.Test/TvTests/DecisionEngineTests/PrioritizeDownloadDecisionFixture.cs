using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DecisionEngine;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.DecisionEngineTests
{
    [TestFixture]
    public class PrioritizeDownloadDecisionFixture : CoreTest<DownloadDecisionPriorizationService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            GivenPreferredDownloadProtocol(DownloadProtocol.Usenet);
            GivenPreferredSize(null);

            _series = Builder<Series>.CreateNew()
                .With(e => e.Runtime = 60)
                .With(e => e.QualityProfile = new QualityProfile
                {
                    Items = Qualities.QualityFixture.GetDefaultQualities()
                })
                .Build();
        }

        private void GivenPreferredSize(double? size)
        {
            Mocker.GetMock<IQualityDefinitionService>()
                  .Setup(s => s.Get(It.IsAny<Quality>()))
                  .Returns(new QualityDefinition { PreferredSize = size });
        }

        [Test]
        public void should_prioritize_tv_decisions()
        {
            Subject.Kind.Should().Be(NzbDrone.Core.Modules.MediaKind.Tv);
        }

        private Episode GivenEpisode(int id)
        {
            return Builder<Episode>.CreateNew()
                            .With(e => e.Id = id)
                            .With(e => e.EpisodeNumber = id)
                            .With(e => e.Runtime = 0)
                            .Build();
        }

        private RemoteEpisode GivenRemoteEpisode(List<Episode> episodes, QualityModel quality, Language language, int age = 0, long size = 0, DownloadProtocol downloadProtocol = DownloadProtocol.Usenet, int indexerPriority = 25)
        {
            var remoteEpisode = new RemoteEpisode();
            remoteEpisode.ParsedEpisodeInfo = new ParsedEpisodeInfo();
            remoteEpisode.ParsedEpisodeInfo.Quality = quality;
            remoteEpisode.ParsedEpisodeInfo.Languages = new List<Language> { language };

            remoteEpisode.Episodes = new List<Episode>();
            remoteEpisode.Episodes.AddRange(episodes);

            remoteEpisode.Release = new ReleaseInfo();
            remoteEpisode.Release.PublishDate = DateTime.Now.AddDays(-age);
            remoteEpisode.Release.Size = size;
            remoteEpisode.Release.DownloadProtocol = downloadProtocol;
            remoteEpisode.Release.IndexerPriority = indexerPriority;

            remoteEpisode.Series = _series;

            return remoteEpisode;
        }

        private void GivenPreferredDownloadProtocol(DownloadProtocol downloadProtocol)
        {
            Mocker.GetMock<IDelayProfileService>()
                  .Setup(s => s.BestForTags(It.IsAny<HashSet<int>>()))
                  .Returns(new DelayProfile
                  {
                      PreferredProtocol = downloadProtocol
                  });
        }

        [Test]
        public void should_put_reals_before_non_reals()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p, new Revision(version: 1, real: 0)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p, new Revision(version: 1, real: 1)), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Real.Should().Be(1);
        }

        [Test]
        public void should_put_propers_before_non_propers()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p, new Revision(version: 1)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p, new Revision(version: 2)), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Version.Should().Be(2);
        }

        [Test]
        public void should_put_higher_quality_before_lower()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Quality.Should().Be(Quality.HDTV720p);
        }

        [Test]
        public void should_order_by_lowest_number_of_episodes()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(2) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Episodes.First().EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_order_by_lowest_number_of_episodes_with_multiple_episodes()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(2), GivenEpisode(3) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1), GivenEpisode(2) }, new QualityModel(Quality.HDTV720p), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Episodes.First().EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_order_by_age_then_largest_rounded_to_200mb()
        {
            var remoteEpisodeSd = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.English, size: 100.Megabytes(), age: 1);
            var remoteEpisodeHdSmallOld = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 1200.Megabytes(), age: 1000);
            var remoteEpisodeSmallYoung = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 1250.Megabytes(), age: 10);
            var remoteEpisodeHdLargeYoung = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 3000.Megabytes(), age: 1);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisodeSd));
            decisions.Add(new DownloadDecision(remoteEpisodeHdSmallOld));
            decisions.Add(new DownloadDecision(remoteEpisodeSmallYoung));
            decisions.Add(new DownloadDecision(remoteEpisodeHdLargeYoung));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisodeHdLargeYoung);
        }

        [Test]
        public void should_order_by_closest_to_preferred_size_if_both_under()
        {
            // 200 MB/Min * 60 Min Runtime = 12000 MB
            GivenPreferredSize(200);

            var remoteEpisodeSmall = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 1200.Megabytes(), age: 1);
            var remoteEpisodeLarge = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 10000.Megabytes(), age: 1);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisodeSmall));
            decisions.Add(new DownloadDecision(remoteEpisodeLarge));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisodeLarge);
        }

        [Test]
        public void should_order_by_closest_to_preferred_size_if_preferred_is_in_between()
        {
            // 46 MB/Min * 60 Min Runtime = 6900 MB
            GivenPreferredSize(46);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 500.Megabytes(), age: 1);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 2000.Megabytes(), age: 1);
            var remoteEpisode3 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 3000.Megabytes(), age: 1);
            var remoteEpisode4 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 5000.Megabytes(), age: 1);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));
            decisions.Add(new DownloadDecision(remoteEpisode3));
            decisions.Add(new DownloadDecision(remoteEpisode4));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisode3);
        }

        [Test]
        public void should_order_by_closest_to_preferred_size_of_the_whole_release()
        {
            // 46 MB/Min * (10 episodes * 60 Min Runtime) = 27600 MB
            GivenPreferredSize(46);

            var episodes = Builder<Episode>.CreateListOfSize(10)
                                           .All()
                                           .With(e => e.Runtime = 60)
                                           .Build()
                                           .ToList();

            var remoteEpisodeSmall = GivenRemoteEpisode(episodes, new QualityModel(Quality.HDTV720p), Language.English, size: 12000.Megabytes(), age: 1);
            var remoteEpisodeLarge = GivenRemoteEpisode(episodes, new QualityModel(Quality.HDTV720p), Language.English, size: 26000.Megabytes(), age: 1);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisodeSmall));
            decisions.Add(new DownloadDecision(remoteEpisodeLarge));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisodeLarge);
        }

        [Test]
        public void should_order_by_youngest()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, age: 10);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, age: 5);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisode2);
        }

        [Test]
        public void should_not_throw_if_no_episodes_are_found()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 500.Megabytes());
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 500.Megabytes());

            remoteEpisode1.Episodes = new List<Episode>();

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            Subject.Prioritize(decisions);
        }

        [Test]
        public void should_put_usenet_above_torrent_when_usenet_is_preferred()
        {
            GivenPreferredDownloadProtocol(DownloadProtocol.Usenet);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, downloadProtocol: DownloadProtocol.Torrent);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, downloadProtocol: DownloadProtocol.Usenet);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Release.DownloadProtocol.Should().Be(DownloadProtocol.Usenet);
        }

        [Test]
        public void should_put_torrent_above_usenet_when_torrent_is_preferred()
        {
            GivenPreferredDownloadProtocol(DownloadProtocol.Torrent);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, downloadProtocol: DownloadProtocol.Torrent);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, downloadProtocol: DownloadProtocol.Usenet);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Release.DownloadProtocol.Should().Be(DownloadProtocol.Torrent);
        }

        [Test]
        public void should_prefer_season_pack_above_single_episode()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1), GivenEpisode(2) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            remoteEpisode1.ParsedEpisodeInfo.FullSeason = true;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.FullSeason.Should().BeTrue();
        }

        [Test]
        public void should_prefer_multiepisode_over_single_episode_for_anime()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1), GivenEpisode(2) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            remoteEpisode1.Series.SeriesType = SeriesTypes.Anime;
            remoteEpisode2.Series.SeriesType = SeriesTypes.Anime;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Episodes.Count.Should().Be(remoteEpisode1.Episodes.Count);
        }

        [Test]
        public void should_prefer_single_episode_over_multi_episode_for_non_anime()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1), GivenEpisode(2) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Episodes.Count.Should().Be(remoteEpisode2.Episodes.Count);
        }

        [Test]
        public void should_prefer_releases_with_more_seeders()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var torrentInfo1 = new TorrentInfo();
            torrentInfo1.PublishDate = DateTime.Now;
            torrentInfo1.Size = 0;
            torrentInfo1.DownloadProtocol = DownloadProtocol.Torrent;
            torrentInfo1.Seeders = 10;

            var torrentInfo2 = torrentInfo1.JsonClone();
            torrentInfo2.Seeders = 100;

            remoteEpisode1.Release = torrentInfo1;
            remoteEpisode2.Release = torrentInfo2;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            ((TorrentInfo)qualifiedReports.First().GetRemoteEpisode().Release).Seeders.Should().Be(torrentInfo2.Seeders);
        }

        [Test]
        public void should_prefer_releases_with_more_peers_given_equal_number_of_seeds()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var torrentInfo1 = new TorrentInfo();
            torrentInfo1.PublishDate = DateTime.Now;
            torrentInfo1.Size = 0;
            torrentInfo1.DownloadProtocol = DownloadProtocol.Torrent;
            torrentInfo1.Seeders = 10;
            torrentInfo1.Peers = 10;

            var torrentInfo2 = torrentInfo1.JsonClone();
            torrentInfo2.Peers = 100;

            remoteEpisode1.Release = torrentInfo1;
            remoteEpisode2.Release = torrentInfo2;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            ((TorrentInfo)qualifiedReports.First().GetRemoteEpisode().Release).Peers.Should().Be(torrentInfo2.Peers);
        }

        [Test]
        public void should_prefer_releases_with_more_peers_no_seeds()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var torrentInfo1 = new TorrentInfo();
            torrentInfo1.PublishDate = DateTime.Now;
            torrentInfo1.Size = 0;
            torrentInfo1.DownloadProtocol = DownloadProtocol.Torrent;
            torrentInfo1.Seeders = 0;
            torrentInfo1.Peers = 10;

            var torrentInfo2 = torrentInfo1.JsonClone();
            torrentInfo2.Seeders = 0;
            torrentInfo2.Peers = 100;

            remoteEpisode1.Release = torrentInfo1;
            remoteEpisode2.Release = torrentInfo2;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            ((TorrentInfo)qualifiedReports.First().GetRemoteEpisode().Release).Peers.Should().Be(torrentInfo2.Peers);
        }

        [Test]
        public void should_prefer_first_release_if_peers_and_size_are_too_similar()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            var torrentInfo1 = new TorrentInfo();
            torrentInfo1.PublishDate = DateTime.Now;
            torrentInfo1.DownloadProtocol = DownloadProtocol.Torrent;
            torrentInfo1.Seeders = 1000;
            torrentInfo1.Peers = 10;
            torrentInfo1.Size = 200.Megabytes();

            var torrentInfo2 = torrentInfo1.JsonClone();
            torrentInfo2.Seeders = 1100;
            torrentInfo2.Peers = 10;
            torrentInfo1.Size = 250.Megabytes();

            remoteEpisode1.Release = torrentInfo1;
            remoteEpisode2.Release = torrentInfo2;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            ((TorrentInfo)qualifiedReports.First().GetRemoteEpisode().Release).Should().Be(torrentInfo1);
        }

        [Test]
        public void should_prefer_first_release_if_age_and_size_are_too_similar()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);

            remoteEpisode1.Release.PublishDate = DateTime.UtcNow.AddDays(-100);
            remoteEpisode1.Release.Size = 200.Megabytes();

            remoteEpisode2.Release.PublishDate = DateTime.UtcNow.AddDays(-150);
            remoteEpisode2.Release.Size = 250.Megabytes();

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Release.Should().Be(remoteEpisode1.Release);
        }

        [Test]
        public void should_prefer_quality_over_the_number_of_peers()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.Bluray1080p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.English);

            var torrentInfo1 = new TorrentInfo();
            torrentInfo1.PublishDate = DateTime.Now;
            torrentInfo1.DownloadProtocol = DownloadProtocol.Torrent;
            torrentInfo1.Seeders = 100;
            torrentInfo1.Peers = 10;
            torrentInfo1.Size = 200.Megabytes();

            var torrentInfo2 = torrentInfo1.JsonClone();
            torrentInfo2.Seeders = 1100;
            torrentInfo2.Peers = 10;
            torrentInfo1.Size = 250.Megabytes();

            remoteEpisode1.Release = torrentInfo1;
            remoteEpisode2.Release = torrentInfo2;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            ((TorrentInfo)qualifiedReports.First().GetRemoteEpisode().Release).Should().Be(torrentInfo1);
        }

        [Test]
        public void should_put_higher_quality_before_lower_always()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.French);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.German);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Quality.Should().Be(Quality.HDTV720p);
        }

        [Test]
        public void should_prefer_higher_score_over_lower_score()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p), Language.English);

            remoteEpisode1.CustomFormatScore = 10;
            remoteEpisode2.CustomFormatScore = 0;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().CustomFormatScore.Should().Be(10);
        }

        [Test]
        public void should_prefer_proper_over_score_when_download_propers_is_prefer_and_upgrade()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DownloadPropersAndRepacks)
                  .Returns(ProperDownloadTypes.PreferAndUpgrade);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(2)), Language.English);

            remoteEpisode1.CustomFormatScore = 10;
            remoteEpisode2.CustomFormatScore = 0;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Version.Should().Be(2);
        }

        [Test]
        public void should_prefer_proper_over_score_when_download_propers_is_do_not_upgrade()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DownloadPropersAndRepacks)
                  .Returns(ProperDownloadTypes.DoNotUpgrade);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(2)), Language.English);

            remoteEpisode1.CustomFormatScore = 10;
            remoteEpisode2.CustomFormatScore = 0;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Version.Should().Be(2);
        }

        [Test]
        public void should_prefer_score_over_proper_when_download_propers_is_do_not_prefer()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DownloadPropersAndRepacks)
                  .Returns(ProperDownloadTypes.DoNotPrefer);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(2)), Language.English);

            remoteEpisode1.CustomFormatScore = 10;
            remoteEpisode2.CustomFormatScore = 0;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Quality.Should().Be(Quality.WEBDL1080p);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Version.Should().Be(1);
            qualifiedReports.First().GetRemoteEpisode().CustomFormatScore.Should().Be(10);
        }

        [Test]
        public void should_prefer_score_over_real_when_download_propers_is_do_not_prefer()
        {
            Mocker.GetMock<IConfigService>()
                  .Setup(s => s.DownloadPropersAndRepacks)
                  .Returns(ProperDownloadTypes.DoNotPrefer);

            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1, 0)), Language.English);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1, 1)), Language.English);

            remoteEpisode1.CustomFormatScore = 10;
            remoteEpisode2.CustomFormatScore = 0;

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode1));
            decisions.Add(new DownloadDecision(remoteEpisode2));

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Quality.Should().Be(Quality.WEBDL1080p);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Version.Should().Be(1);
            qualifiedReports.First().GetRemoteEpisode().ParsedEpisodeInfo.Quality.Revision.Real.Should().Be(0);
            qualifiedReports.First().GetRemoteEpisode().CustomFormatScore.Should().Be(10);
        }

        [Test]
        public void sort_download_decisions_based_on_indexer_priority()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English, indexerPriority: 25);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English, indexerPriority: 50);
            var remoteEpisode3 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English, indexerPriority: 1);

            var decisions = new List<DownloadDecision>();
            decisions.AddRange(new[] { new DownloadDecision(remoteEpisode1), new DownloadDecision(remoteEpisode2), new DownloadDecision(remoteEpisode3) });

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisode3);
            qualifiedReports.Skip(1).First().GetRemoteEpisode().Should().Be(remoteEpisode1);
            qualifiedReports.Last().GetRemoteEpisode().Should().Be(remoteEpisode2);
        }

        [Test]
        public void ensure_download_decisions_indexer_priority_is_not_perfered_over_quality()
        {
            var remoteEpisode1 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p, new Revision(1)), Language.English, indexerPriority: 25);
            var remoteEpisode2 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English, indexerPriority: 50);
            var remoteEpisode3 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV, new Revision(1)), Language.English, indexerPriority: 1);
            var remoteEpisode4 = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.WEBDL1080p, new Revision(1)), Language.English, indexerPriority: 25);

            var decisions = new List<DownloadDecision>();
            decisions.AddRange(new[] { new DownloadDecision(remoteEpisode1), new DownloadDecision(remoteEpisode2), new DownloadDecision(remoteEpisode3), new DownloadDecision(remoteEpisode4) });

            var qualifiedReports = Subject.Prioritize(decisions);
            qualifiedReports.First().GetRemoteEpisode().Should().Be(remoteEpisode4);
            qualifiedReports.Skip(1).First().GetRemoteEpisode().Should().Be(remoteEpisode2);
            qualifiedReports.Skip(2).First().GetRemoteEpisode().Should().Be(remoteEpisode1);
            qualifiedReports.Last().GetRemoteEpisode().Should().Be(remoteEpisode3);
        }

        [Test]
        public void should_prefer_a_release_less_than_an_hour_old_over_one_a_few_hours_old_on_usenet()
        {
            var fresh = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 100.Megabytes());
            var older = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 900.Megabytes());

            fresh.Release.PublishDate = DateTime.UtcNow.AddMinutes(-30);
            older.Release.PublishDate = DateTime.UtcNow.AddHours(-5);

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(older), new DownloadDecision(fresh) });

            qualifiedReports.First().GetRemoteEpisode().Should().BeSameAs(fresh);
        }

        [Test]
        public void should_prefer_a_release_a_week_old_over_one_three_weeks_old_on_usenet()
        {
            var week = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, age: 7, size: 100.Megabytes());
            var weeks = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, age: 20, size: 900.Megabytes());

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(weeks), new DownloadDecision(week) });

            qualifiedReports.First().GetRemoteEpisode().Should().BeSameAs(week);
        }

        [Test]
        public void should_prefer_the_largest_release_when_a_preferred_size_is_set_but_the_runtime_is_unknown()
        {
            GivenPreferredSize(50);

            _series.Runtime = 0;

            var small = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 100.Megabytes());
            var large = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 4000.Megabytes());

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(small), new DownloadDecision(large) });

            qualifiedReports.First().GetRemoteEpisode().Should().BeSameAs(large);
        }

        [Test]
        public void should_prefer_the_higher_custom_format_score_over_a_better_indexer_priority_and_size()
        {
            var scored = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 100.Megabytes(), indexerPriority: 50);
            var unscored = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English, size: 900.Megabytes(), indexerPriority: 1);

            scored.CustomFormatScore = 10;
            unscored.CustomFormatScore = 0;

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(unscored), new DownloadDecision(scored) });

            qualifiedReports.First().GetRemoteEpisode().Should().BeSameAs(scored);
        }

        [Test]
        public void should_keep_decisions_without_a_series_after_the_ones_with_a_series()
        {
            var unknown = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.Bluray1080p), Language.English);
            unknown.Series = null;

            var known = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.English);

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(unknown), new DownloadDecision(known) });

            qualifiedReports.Should().HaveCount(2);
            qualifiedReports.First().GetRemoteEpisode().Should().BeSameAs(known);
            qualifiedReports.Last().GetRemoteEpisode().Should().BeSameAs(unknown);
        }

        [Test]
        public void should_order_within_each_series_and_keep_series_in_the_order_they_first_appear()
        {
            var otherSeries = Builder<Series>.CreateNew()
                .With(s => s.Id = _series.Id + 100)
                .With(s => s.Runtime = 60)
                .With(s => s.QualityProfile = new QualityProfile { Items = Qualities.QualityFixture.GetDefaultQualities() })
                .Build();

            var firstLow = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.SDTV), Language.English);
            var firstHigh = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.HDTV720p), Language.English);
            var secondHigh = GivenRemoteEpisode(new List<Episode> { GivenEpisode(1) }, new QualityModel(Quality.Bluray1080p), Language.English);

            secondHigh.Series = otherSeries;

            var qualifiedReports = Subject.Prioritize(new List<DownloadDecision> { new DownloadDecision(firstLow), new DownloadDecision(secondHigh), new DownloadDecision(firstHigh) });

            qualifiedReports.Select(d => d.GetRemoteEpisode()).Should().Equal(firstHigh, firstLow, secondHigh);
        }
    }
}
