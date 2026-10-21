using NzbDrone.Core.Housekeeping;
using NzbDrone.Core.Housekeeping.Housekeepers;
using NzbDrone.Core.ImportLists;

namespace NzbDrone.Core.Movies.Housekeepers
{
    public class FixFutureImportListStatusTimes : FixFutureProviderStatusTimes<ImportListStatus>, IHousekeepingTask
    {
        public FixFutureImportListStatusTimes(IImportListStatusRepository importListStatusRepository)
            : base(importListStatusRepository)
        {
        }
    }
}
