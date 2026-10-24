using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.DecisionEngine.Specifications.RssSync
{
    public class HistorySpecification : IDownloadDecisionEngineSpecification
    {
        private readonly IHistoryService _historyService;
        private readonly UpgradableSpecification _upgradableSpecification;
        private readonly IMusicCustomFormatCalculationService _formatService;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public HistorySpecification(IHistoryService historyService,
                                    UpgradableSpecification upgradableSpecification,
                                    IMusicCustomFormatCalculationService formatService,
                                    IConfigService configService,
                                    Logger logger)
        {
            _historyService = historyService;
            _upgradableSpecification = upgradableSpecification;
            _formatService = formatService;
            _configService = configService;
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Database;
        public RejectionType Type => RejectionType.Permanent;

        public virtual DownloadSpecDecision IsSatisfiedBy(RemoteAlbum subject, ReleaseDecisionInformation information)
        {
            if (information.SearchCriteria != null)
            {
                _logger.Debug("Skipping history check during search");
                return DownloadSpecDecision.Accept();
            }

            var cdhEnabled = _configService.EnableCompletedDownloadHandling;

            _logger.Debug("Performing history status check on report");

            foreach (var album in subject.Albums)
            {
                _logger.Debug("Checking current status of album [{0}] in history", album.Id);
                var mostRecent = _historyService.MostRecentForAlbum(album.Id);

                if (mostRecent != null && mostRecent.EventType == AlbumHistoryEventType.Grabbed)
                {
                    var recent = mostRecent.Date.After(DateTime.UtcNow.AddHours(-12));

                    if (!recent && cdhEnabled)
                    {
                        continue;
                    }

                    var customFormats = _formatService.ParseCustomFormat(mostRecent, subject.Artist);

                    // The artist will be the same as the one in history since it's the same album.
                    // Instead of fetching the artist from the DB reuse the known artist.
                    var cutoffUnmet = _upgradableSpecification.CutoffNotMet(
                        subject.Artist.QualityProfile,
                        new List<AudioQualityModel> { mostRecent.Quality },
                        customFormats,
                        subject.ParsedAlbumInfo.Quality);

                    var upgradeable = _upgradableSpecification.IsUpgradable(
                        subject.Artist.QualityProfile,
                        new List<AudioQualityModel> { mostRecent.Quality },
                        customFormats,
                        subject.ParsedAlbumInfo.Quality,
                        subject.CustomFormats);

                    if (!cutoffUnmet)
                    {
                        if (recent)
                        {
                            return DownloadSpecDecision.Reject(DownloadRejectionReason.HistoryRecentCutoffMet, "Recent grab event in history already meets cutoff: {0}", mostRecent.Quality);
                        }

                        return DownloadSpecDecision.Reject(DownloadRejectionReason.HistoryCdhDisabledCutoffMet, "CDH is disabled and grab event in history already meets cutoff: {0}", mostRecent.Quality);
                    }

                    if (!upgradeable)
                    {
                        if (recent)
                        {
                            return DownloadSpecDecision.Reject(DownloadRejectionReason.HistoryHigherPreference, "Recent grab event in history is of equal or higher quality: {0}", mostRecent.Quality);
                        }

                        return DownloadSpecDecision.Reject(DownloadRejectionReason.HistoryHigherPreference, "CDH is disabled and grab event in history is of equal or higher quality: {0}", mostRecent.Quality);
                    }
                }
            }

            return DownloadSpecDecision.Accept();
        }
    }
}
