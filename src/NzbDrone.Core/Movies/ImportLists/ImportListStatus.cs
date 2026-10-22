using System;
using NzbDrone.Core.ThingiProvider.Status;

namespace NzbDrone.Core.Movies.ImportLists
{
    public class ImportListStatus : ProviderStatusBase
    {
        public DateTime? LastInfoSync { get; set; }
    }
}
