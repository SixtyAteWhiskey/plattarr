using System;
using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Test.Architecture
{
    // Which types in NzbDrone.Core belong to a media module. Everything else is core.
    //
    // A module owns NzbDrone.Core.<Module>.* (spec, "Boundary rules"). Until step 11 some Radarr
    // namespaces and parser types were listed here by name because they had not moved under
    // NzbDrone.Core.Movies yet; they have all moved, so the boundary is now purely the namespace rule.
    public static class ModuleBoundary
    {
        public static readonly Dictionary<MediaKind, string> ModuleNamespaces = new()
        {
            { MediaKind.Movies, "NzbDrone.Core.Movies" },
            { MediaKind.Tv, "NzbDrone.Core.Tv" },
            { MediaKind.Music, "NzbDrone.Core.Music" }
        };

        public static MediaKind? ModuleOf(Type type)
        {
            var space = type.Namespace ?? string.Empty;

            foreach (var (kind, prefix) in ModuleNamespaces)
            {
                if (InNamespace(space, prefix))
                {
                    return kind;
                }
            }

            return null;
        }

        public static bool InNamespace(string space, string prefix)
        {
            return space == prefix || space.StartsWith(prefix + ".", StringComparison.Ordinal);
        }
    }
}
