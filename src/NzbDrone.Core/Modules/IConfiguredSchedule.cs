using System.Collections.Generic;
using NzbDrone.Core.Configuration;

namespace NzbDrone.Core.Modules
{
    public interface IConfiguredSchedule
    {
        IReadOnlyDictionary<string, int> ConfiguredIntervals(IConfigService config);
    }
}
