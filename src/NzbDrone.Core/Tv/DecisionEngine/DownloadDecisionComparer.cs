using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Tv.DecisionEngine
{
    // Sonarr's DownloadDecisionComparer. Sonarr has no indexer flag comparison and neither has this.
    public class DownloadDecisionComparer : IComparer<DownloadDecision>
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IQualityDefinitionService _qualityDefinitionService;

        public delegate int CompareDelegate(DownloadDecision x, DownloadDecision y);
        public delegate int CompareDelegate<TSubject, TValue>(DownloadDecision x, DownloadDecision y);

        public DownloadDecisionComparer(IConfigService configService, IDelayProfileService delayProfileService, IQualityDefinitionService qualityDefinitionService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
        }

        public int Compare(DownloadDecision x, DownloadDecision y)
        {
            var comparers = new List<CompareDelegate>
            {
                CompareQuality,
                CompareCustomFormatScore,
                CompareProtocol,
                CompareEpisodeCount,
                CompareEpisodeNumber,
                CompareIndexerPriority,
                ComparePeersIfTorrent,
                CompareAgeIfUsenet,
                CompareSize
            };

            return comparers.Select(comparer => comparer(x, y)).FirstOrDefault(result => result != 0);
        }

        private int CompareBy<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            var leftValue = funcValue(left);
            var rightValue = funcValue(right);

            return leftValue.CompareTo(rightValue);
        }

        private int CompareByReverse<TSubject, TValue>(TSubject left, TSubject right, Func<TSubject, TValue> funcValue)
            where TValue : IComparable<TValue>
        {
            return CompareBy(left, right, funcValue) * -1;
        }

        private int CompareAll(params int[] comparers)
        {
            return comparers.Select(comparer => comparer).FirstOrDefault(result => result != 0);
        }

        private int CompareIndexerPriority(DownloadDecision x, DownloadDecision y)
        {
            return CompareByReverse(x.GetRemoteEpisode().Release, y.GetRemoteEpisode().Release, release => release.IndexerPriority);
        }

        private int CompareQuality(DownloadDecision x, DownloadDecision y)
        {
            if (_configService.DownloadPropersAndRepacks == ProperDownloadTypes.DoNotPrefer)
            {
                return CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.Series.QualityProfile.Value.GetIndex(remoteEpisode.ParsedEpisodeInfo.Quality.Quality));
            }

            return CompareAll(
                CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.Series.QualityProfile.Value.GetIndex(remoteEpisode.ParsedEpisodeInfo.Quality.Quality)),
                CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.ParsedEpisodeInfo.Quality.Revision));
        }

        private int CompareCustomFormatScore(DownloadDecision x, DownloadDecision y)
        {
            return CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.CustomFormatScore);
        }

        private int CompareProtocol(DownloadDecision x, DownloadDecision y)
        {
            var result = CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode =>
            {
                var delayProfile = _delayProfileService.BestForTags(remoteEpisode.Series.Tags);
                var downloadProtocol = remoteEpisode.Release.DownloadProtocol;
                return downloadProtocol == delayProfile.PreferredProtocol;
            });

            return result;
        }

        private int CompareEpisodeCount(DownloadDecision x, DownloadDecision y)
        {
            var seasonPackCompare = CompareBy(x.GetRemoteEpisode(),
                y.GetRemoteEpisode(),
                remoteEpisode => remoteEpisode.ParsedEpisodeInfo.FullSeason);

            if (seasonPackCompare != 0)
            {
                return seasonPackCompare;
            }

            if (x.GetRemoteEpisode().Series.SeriesType == SeriesTypes.Anime &
                y.GetRemoteEpisode().Series.SeriesType == SeriesTypes.Anime)
            {
                return CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.Episodes.Count);
            }

            return CompareByReverse(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.Episodes.Count);
        }

        private int CompareEpisodeNumber(DownloadDecision x, DownloadDecision y)
        {
            return CompareByReverse(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode => remoteEpisode.Episodes.Select(e => e.EpisodeNumber).MinOrDefault());
        }

        private int ComparePeersIfTorrent(DownloadDecision x, DownloadDecision y)
        {
            // Different protocols should get caught when checking the preferred protocol,
            // since we're dealing with the same series in our comparisons
            if (x.GetRemoteEpisode().Release.DownloadProtocol != DownloadProtocol.Torrent ||
                y.GetRemoteEpisode().Release.DownloadProtocol != DownloadProtocol.Torrent)
            {
                return 0;
            }

            return CompareAll(
                CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode =>
                {
                    var seeders = TorrentInfo.GetSeeders(remoteEpisode.Release);

                    return seeders.HasValue && seeders.Value > 0 ? Math.Round(Math.Log10(seeders.Value)) : 0;
                }),
                CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode =>
                {
                    var peers = TorrentInfo.GetPeers(remoteEpisode.Release);

                    return peers.HasValue && peers.Value > 0 ? Math.Round(Math.Log10(peers.Value)) : 0;
                }));
        }

        private int CompareAgeIfUsenet(DownloadDecision x, DownloadDecision y)
        {
            if (x.GetRemoteEpisode().Release.DownloadProtocol != DownloadProtocol.Usenet ||
                y.GetRemoteEpisode().Release.DownloadProtocol != DownloadProtocol.Usenet)
            {
                return 0;
            }

            return CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode =>
            {
                var ageHours = remoteEpisode.Release.AgeHours;
                var age = remoteEpisode.Release.Age;

                if (ageHours < 1)
                {
                    return 1000;
                }

                if (ageHours <= 24)
                {
                    return 100;
                }

                if (age <= 7)
                {
                    return 10;
                }

                return Math.Round(Math.Log10(age)) * -1;
            });
        }

        private int CompareSize(DownloadDecision x, DownloadDecision y)
        {
            var sizeCompare = CompareBy(x.GetRemoteEpisode(), y.GetRemoteEpisode(), remoteEpisode =>
            {
                // The preferred size is on the shared quality definition, in MB per minute.
                var preferredSize = _qualityDefinitionService.Get(remoteEpisode.ParsedEpisodeInfo.Quality.Quality).PreferredSize;
                var runtime = GetRuntime(remoteEpisode);
                var size = remoteEpisode.Release.Size.GetValueOrDefault();

                // If no value for preferred it means unlimited so fallback to sort largest is best
                if (preferredSize.HasValue && runtime > 0)
                {
                    var preferredReleaseSize = runtime * preferredSize.Value.Megabytes();

                    // Calculate closest to the preferred size
                    return Math.Abs((size - preferredReleaseSize).Round(200.Megabytes())) * (-1);
                }
                else
                {
                    return size.Round(200.Megabytes());
                }
            });

            return sizeCompare;
        }

        private static int GetRuntime(RemoteEpisode remoteEpisode)
        {
            if (remoteEpisode.Episodes.Count == 0)
            {
                return remoteEpisode.Series.Runtime;
            }

            return remoteEpisode.Episodes.Sum(episode => episode.Runtime > 0 ? episode.Runtime : remoteEpisode.Series.Runtime);
        }
    }
}
