using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore
{
    [TestFixture]
    public class SortKeyValidationFixture : DbTest
    {
        [TestCase("amissingcolumn")]
        [TestCase("amissingtable.id")]
        [TestCase("table.table.column")]
        [TestCase("column; DROP TABLE Commands;--")]
        public void should_return_false_for_invalid_sort_key(string sortKey)
        {
            TableMapping.Mapper.IsValidSortKey(sortKey).Should().BeFalse();
        }

        [TestCase("Id")]
        [TestCase("id")]
        [TestCase("commands.id")]
        [TestCase("movieMetadata.sortTitle")]
        [TestCase("movies.lastSearchTime")]
        [TestCase("Movies_MovieMetadata.sortTitle")]
        public void should_return_true_for_valid_sort_key(string sortKey)
        {
            TableMapping.Mapper.IsValidSortKey(sortKey).Should().BeTrue();
        }

        [TestCase("movieMetadata.sortTitle", "Movies_MovieMetadata", "SortTitle")]
        [TestCase("movies.lastSearchTime", "Movies_Movies", "LastSearchTime")]
        [TestCase("commands.id", "Commands", "Id")]
        public void should_resolve_upstream_table_names_to_module_tables(string sortKey, string table, string column)
        {
            TableMapping.Mapper.GetSortKey(sortKey).Should().Be((table, column));
        }
    }
}
