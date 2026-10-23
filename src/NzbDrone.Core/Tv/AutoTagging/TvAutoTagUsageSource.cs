using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.AutoTagging.Specifications;

namespace NzbDrone.Core.Tv.AutoTagging
{
    public class TvAutoTagUsageSource : IAutoTagUsageSource
    {
        private readonly IAutoTaggingService _autoTaggingService;

        public TvAutoTagUsageSource(IAutoTaggingService autoTaggingService)
        {
            _autoTaggingService = autoTaggingService;
        }

        public MediaKind Kind => MediaKind.Tv;

        public List<AutoTagUsage> All()
        {
            return _autoTaggingService.All()
                .Select(a => new AutoTagUsage
                {
                    Id = a.Id,
                    Tags = a.Tags.ToList(),
                    ConditionTags = a.Specifications.OfType<TagSpecification>().Select(s => s.Value).ToList()
                })
                .ToList();
        }
    }
}
