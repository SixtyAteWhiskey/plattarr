using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Profiles.Delay;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.DecisionEngine
{
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

        public MediaKind Kind => MediaKind.Movies;

        public List<DownloadDecision> Prioritize(List<DownloadDecision> decisions)
        {
            return decisions.Where(c => c.GetRemoteMovie().Movie != null)
                            .GroupBy(c => c.GetRemoteMovie().Movie.Id, (movieId, downloadDecisions) =>
                            {
                                return downloadDecisions.OrderByDescending(decision => decision, new DownloadDecisionComparer(_configService, _delayProfileService, _qualityDefinitionService));
                            })
                            .SelectMany(c => c)
                            .Union(decisions.Where(c => c.GetRemoteMovie().Movie == null))
                            .ToList();
        }
    }
}
