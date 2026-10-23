using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Tv.DecisionEngine
{
    // Sonarr's DownloadDecisionPriorizationService behind the module contract: the core splits the
    // decisions by media type and hands the TV ones here.
    public class DownloadDecisionPriorizationService : IDownloadDecisionPrioritizer
    {
        private readonly IConfigService _configService;
        private readonly IDelayProfileService _delayProfileService;
        private readonly IQualityDefinitionService _qualityDefinitionService;

        public DownloadDecisionPriorizationService(IConfigService configService, IDelayProfileService delayProfileService, IQualityDefinitionService qualityDefinitionService)
        {
            _configService = configService;
            _delayProfileService = delayProfileService;
            _qualityDefinitionService = qualityDefinitionService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public List<DownloadDecision> Prioritize(List<DownloadDecision> decisions)
        {
            return decisions.Where(c => c.GetRemoteEpisode().Series != null)
                            .GroupBy(c => c.GetRemoteEpisode().Series.Id, (seriesId, downloadDecisions) =>
                            {
                                return downloadDecisions.OrderByDescending(decision => decision, new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService));
                            })
                            .SelectMany(c => c)
                            .Union(decisions.Where(c => c.GetRemoteEpisode().Series == null))
                            .ToList();
        }
    }
}
