using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Tags;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class TagUsageFixture : CoreTest
    {
        // Tables whose entity has a Tags property that is stored as a column, read from the
        // real table mapping. A property the mapper ignores (metadata consumers) has no
        // column, and querying it would fail, so it must not be in the list.
        private static List<string> TablesWithTagsColumn()
        {
            RuntimeHelpers.RunClassConstructor(typeof(DbFactory).TypeHandle);

            return TableMapping.Mapper.TableMap
                .Where(pair =>
                {
                    var property = pair.Key.GetProperty("Tags");

                    return property != null &&
                           property.PropertyType == typeof(HashSet<int>) &&
                           !TableMapping.Mapper.ExcludeProperties(pair.Key).Any(p => p.Name == "Tags");
                })
                .Select(pair => pair.Value)
                .OrderBy(table => table)
                .ToList();
        }

        [Test]
        public void tag_usage_should_list_every_mapped_table_with_a_tags_column()
        {
            TablesWithTagsColumn().Should().BeEquivalentTo(TagUsage.Tables);
        }

        [Test]
        public void tag_usage_should_list_each_table_once()
        {
            TagUsage.Tables.Should().OnlyHaveUniqueItems();
        }
    }
}
