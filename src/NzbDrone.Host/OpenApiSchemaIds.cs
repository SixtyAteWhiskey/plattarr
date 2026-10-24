using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Host
{
    /// <summary>
    /// Schema ids for the generated OpenAPI document. Swashbuckle uses the simple type name and throws when two
    /// types in the document share one. The Movies, Tv and Music modules define some Core types with the same
    /// name (for example <c>MonitorTypes</c>), so:
    /// <list type="bullet">
    /// <item>a Core type in <c>NzbDrone.Core.Music</c> whose name is also used by a public Core type outside that
    /// namespace gets a <c>Music</c> prefix;</item>
    /// <item>a Core type in <c>NzbDrone.Core.Tv</c> whose name is also used by a public Core type outside both the
    /// Tv and the Music namespaces gets a <c>Tv</c> prefix.</item>
    /// </list>
    /// Every other type keeps the id that Swashbuckle would have chosen, so the Radarr-compatible schema names and
    /// the Tv ids do not change when a module is added.
    /// </summary>
    public static class OpenApiSchemaIds
    {
        private const string TvNamespace = "NzbDrone.Core.Tv";
        private const string TvPrefix = "Tv";
        private const string MusicNamespace = "NzbDrone.Core.Music";
        private const string MusicPrefix = "Music";

        private static readonly Lazy<HashSet<string>> SharedTvNames = new Lazy<HashSet<string>>(() => FindSharedNames(TvNamespace, MusicNamespace));
        private static readonly Lazy<HashSet<string>> SharedMusicNames = new Lazy<HashSet<string>>(() => FindSharedNames(MusicNamespace));

        public static string Get(Type type)
        {
            if (!type.IsConstructedGenericType)
            {
                var name = type.Name.Replace("[]", "Array");

                if (InNamespace(type, TvNamespace) && SharedTvNames.Value.Contains(type.Name))
                {
                    return TvPrefix + name;
                }

                if (InNamespace(type, MusicNamespace) && SharedMusicNames.Value.Contains(type.Name))
                {
                    return MusicPrefix + name;
                }

                return name;
            }

            var arguments = string.Concat(type.GetGenericArguments().Select(Get));

            return arguments + type.Name.Split('`').First();
        }

        private static bool InNamespace(Type type, string root)
        {
            return type.Namespace != null &&
                   (type.Namespace == root || type.Namespace.StartsWith(root + ".", StringComparison.Ordinal));
        }

        // The names of the public Core types in the namespace that another public Core type also uses, where
        // the other types are those outside the namespace and outside the ignored ones.
        private static HashSet<string> FindSharedNames(string own, params string[] ignored)
        {
            var types = typeof(NzbDrone.Core.Tv.Series).Assembly.GetExportedTypes();

            var otherNames = types
                .Where(t => !InNamespace(t, own) && !ignored.Any(i => InNamespace(t, i)))
                .Select(t => t.Name)
                .ToHashSet();

            return types
                .Where(t => InNamespace(t, own))
                .Select(t => t.Name)
                .Where(otherNames.Contains)
                .ToHashSet();
        }
    }
}
