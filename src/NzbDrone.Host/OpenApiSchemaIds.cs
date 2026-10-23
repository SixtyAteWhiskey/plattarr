using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Tv;

namespace NzbDrone.Host
{
    /// <summary>
    /// Schema ids for the generated OpenAPI document. Swashbuckle uses the simple type name and throws when two
    /// types in the document share one. The Movies and Tv modules both define some Core types with the same
    /// name (for example <c>MonitorTypes</c>), so a Core type in <c>NzbDrone.Core.Tv</c> whose name is also used
    /// by a public Core type outside that namespace gets a <c>Tv</c> prefix. Every other type keeps the id that
    /// Swashbuckle would have chosen, so the Radarr-compatible schema names do not change.
    /// </summary>
    public static class OpenApiSchemaIds
    {
        private const string TvNamespace = "NzbDrone.Core.Tv";
        private const string TvPrefix = "Tv";

        private static readonly Lazy<HashSet<string>> SharedNames = new Lazy<HashSet<string>>(FindSharedNames);

        public static string Get(Type type)
        {
            if (!type.IsConstructedGenericType)
            {
                var name = type.Name.Replace("[]", "Array");

                return IsTvCoreType(type) && SharedNames.Value.Contains(type.Name)
                    ? TvPrefix + name
                    : name;
            }

            var arguments = string.Concat(type.GetGenericArguments().Select(Get));

            return arguments + type.Name.Split('`').First();
        }

        private static bool IsTvCoreType(Type type)
        {
            return type.Namespace != null &&
                   (type.Namespace == TvNamespace || type.Namespace.StartsWith(TvNamespace + ".", StringComparison.Ordinal));
        }

        private static HashSet<string> FindSharedNames()
        {
            var types = typeof(Series).Assembly.GetExportedTypes();

            var otherNames = types
                .Where(t => !IsTvCoreType(t))
                .Select(t => t.Name)
                .ToHashSet();

            return types
                .Where(IsTvCoreType)
                .Select(t => t.Name)
                .Where(otherNames.Contains)
                .ToHashSet();
        }
    }
}
