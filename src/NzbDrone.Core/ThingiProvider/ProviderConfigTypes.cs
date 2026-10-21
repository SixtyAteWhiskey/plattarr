using System;
using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.ThingiProvider
{
    public static class ProviderConfigTypes
    {
        private static readonly Lazy<ILookup<string, Type>> TypesByName = new(() =>
            typeof(IProviderConfig).Assembly.GetExportedTypes()
                .Where(t => typeof(IProviderConfig).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
                .ToLookup(t => t.Name, StringComparer.InvariantCultureIgnoreCase));

        // Resolves a stored ConfigContract (a settings class name) to its type.
        // Two provider families can declare settings classes with the same name: during the
        // indexer port, Radarr's legacy layer and Prowlarr's layer both had NewznabSettings,
        // TorznabSettings, and six others, and the Sonarr and Lidarr ports bring notification
        // and import list settings that Radarr also has. A provider's settings live under its
        // definition's namespace, so a name match is narrowed to that namespace.
        public static Type Find(string configContract, Type definitionType)
        {
            if (string.IsNullOrWhiteSpace(configContract))
            {
                return null;
            }

            return Resolve(configContract, definitionType, TypesByName.Value[configContract]);
        }

        // The rule behind Find, over any set of candidate types with the contract's name.
        public static Type Resolve(string configContract, Type definitionType, IEnumerable<Type> candidates)
        {
            var matches = candidates.ToList();

            if (matches.Count <= 1)
            {
                return matches.SingleOrDefault();
            }

            var definitionNamespace = definitionType.Namespace;
            var scoped = matches
                .Where(t => t.Namespace == definitionNamespace || t.Namespace.StartsWith(definitionNamespace + ".", StringComparison.Ordinal))
                .ToList();

            if (scoped.Count == 1)
            {
                return scoped[0];
            }

            throw new InvalidOperationException($"Config contract '{configContract}' matches {matches.Count} settings types and {scoped.Count} under {definitionNamespace}");
        }
    }
}
