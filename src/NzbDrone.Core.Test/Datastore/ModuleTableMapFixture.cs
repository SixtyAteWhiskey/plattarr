using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Reflection;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class ModuleTableMapFixture
    {
        // Shared tables a module maps its own view of (spec, contract 6 and "Other tables").
        private static readonly HashSet<string> SharedTables = new()
        {
            "History",
            "Blocklist",
            "PendingReleases",
            "ImportExclusions",
            "AutoTagging"
        };

        private static List<IModuleTableMap> TableMaps()
        {
            return typeof(IModuleTableMap).Assembly.ImplementationsOf<IModuleTableMap>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => (IModuleTableMap)Activator.CreateInstance(t))
                .ToList();
        }

        private static List<IMediaModule> Modules()
        {
            return typeof(IMediaModule).Assembly.ImplementationsOf<IMediaModule>()
                .Where(t => t.IsClass && !t.IsAbstract)
                .Select(t => (IMediaModule)Activator.CreateInstance(t))
                .ToList();
        }

        [Test]
        public void every_module_should_have_one_table_map_with_its_prefix()
        {
            var maps = TableMaps();
            var modules = Modules();

            modules.Should().NotBeEmpty();
            maps.Select(m => m.Kind).Should().OnlyHaveUniqueItems();
            maps.Select(m => m.Kind).Should().BeEquivalentTo(modules.Select(m => m.Kind));

            foreach (var module in modules)
            {
                maps.Single(m => m.Kind == module.Kind).TablePrefix.Should().Be(module.Name + "_");
            }
        }

        [Test]
        public void module_tables_should_be_prefixed_or_shared()
        {
            foreach (var map in TableMaps())
            {
                var mapper = new TableMapper();
                map.MapTables(mapper);

                mapper.TableMap.Values
                    .Where(t => !t.StartsWith(map.TablePrefix, StringComparison.Ordinal))
                    .Should().BeSubsetOf(SharedTables, $"{map.Kind} tables start with {map.TablePrefix} unless shared");
            }
        }

        [Test]
        public void table_mapping_should_include_module_tables_and_prefixes()
        {
            RuntimeHelpers.RunClassConstructor(typeof(DbFactory).TypeHandle);

            TableMapping.Mapper.ModuleTablePrefixes.Should().Equal("Movies_");
            TableMapping.Mapper.TableMap.Values.Should().Contain(new[] { "Movies_Movies", "History", "PendingReleases" });
        }
    }
}
