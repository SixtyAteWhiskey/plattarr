using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Upstream
{
    public static class UpstreamCatalogs
    {
        // The registered catalog of one source app; a stage that reads that app's rows needs it.
        public static IUpstreamCatalog For(this IEnumerable<IUpstreamCatalog> catalogs, string app)
        {
            return catalogs.Single(c => string.Equals(c.AppName, app, StringComparison.OrdinalIgnoreCase));
        }
    }
}
