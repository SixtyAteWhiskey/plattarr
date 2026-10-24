using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Profiles.Delay;

namespace NzbDrone.Core.Music.DecisionEngine
{
    // Lidarr's DownloadDecisionPriorizationService behind the module contract: the core splits the
    // decisions by media type and hands the Music ones here. As in Lidarr, decisions are ordered per
    // artist and only those that allow a download are ordered; the rest follow unordered.
    public class DownloadDecisionPriorizationService : IDownloadDecisionPrioritizer
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IAudioQualityDefinitionService _qualityDefinitionService;
        private readonly IAlbumYearMatcher _albumYearMatcher;

        public DownloadDecisionPriorizationService(IConfigService configService,
                                                   IDelayProfileService delayProfileService,
                                                   IAudioQualityDefinitionService qualityDefinitionService,
                                                   IAlbumYearMatcher albumYearMatcher)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
            _albumYearMatcher = albumYearMatcher;
        }

        public MediaKind Kind => MediaKind.Music;

        public List<DownloadDecision> Prioritize(List<DownloadDecision> decisions)
        {
            return decisions.Where(c => c.GetRemoteAlbum().DownloadAllowed)
                            .GroupBy(c => c.GetRemoteAlbum().Artist.Id, (artistId, downloadDecisions) =>
                            {
                                return downloadDecisions.OrderByDescending(decision => decision, new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService, _albumYearMatcher));
                            })
                            .SelectMany(c => c)
                            .Union(decisions.Where(c => !c.GetRemoteAlbum().DownloadAllowed))
                            .ToList();
        }
    }
}
