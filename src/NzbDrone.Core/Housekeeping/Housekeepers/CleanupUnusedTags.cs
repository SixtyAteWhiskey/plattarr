using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.AutoTagging;
using NzbDrone.Core.Movies.AutoTagging.Specifications;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Housekeeping.Housekeepers
{
    public class CleanupUnusedTags : IHousekeepingTask
    {
        private readonly IMainDatabase _database;
        private readonly IAutoTaggingRepository _autoTaggingRepository;
        private readonly IEnumerable<IAutoTagUsageSource> _autoTagUsageSources;

        public CleanupUnusedTags(IMainDatabase database, IAutoTaggingRepository autoTaggingRepository, IEnumerable<IAutoTagUsageSource> autoTagUsageSources)
        {
            _database = database;
            _autoTaggingRepository = autoTaggingRepository;
            _autoTagUsageSources = autoTagUsageSources;
        }

        public void Clean()
        {
            using var mapper = _database.OpenConnection();
            var usedTags = TagUsage.Tables
                .SelectMany(v => GetUsedTags(v, mapper))
                .Concat(GetAutoTaggingTagSpecificationTags(mapper))
                .Concat(_autoTagUsageSources.SelectMany(s => s.All()).SelectMany(a => a.ConditionTags))
                .Distinct()
                .ToList();

            if (usedTags.Any())
            {
                var usedTagsList = usedTags.Select(d => d.ToString()).Join(",");

                if (_database.DatabaseType == DatabaseType.PostgreSQL)
                {
                    mapper.Execute($"DELETE FROM \"Tags\" WHERE NOT \"Id\" = ANY (\'{{{usedTagsList}}}\'::int[])");
                }
                else
                {
                    mapper.Execute($"DELETE FROM \"Tags\" WHERE NOT \"Id\" IN ({usedTagsList})");
                }
            }
            else
            {
                mapper.Execute("DELETE FROM \"Tags\"");
            }
        }

        private int[] GetUsedTags(string table, IDbConnection mapper)
        {
            return mapper
                .Query<List<int>>(
                    $"SELECT DISTINCT \"Tags\" FROM \"{table}\" WHERE NOT \"Tags\" = '[]' AND NOT \"Tags\" IS NULL")
                .SelectMany(x => x)
                .Distinct()
                .ToArray();
        }

        private List<int> GetAutoTaggingTagSpecificationTags(IDbConnection mapper)
        {
            var tags = new List<int>();
            var autoTags = _autoTaggingRepository.All();

            foreach (var autoTag in autoTags)
            {
                foreach (var specification in autoTag.Specifications)
                {
                    if (specification is TagSpecification tagSpec)
                    {
                        tags.Add(tagSpec.Value);
                    }
                }
            }

            return tags;
        }
    }
}
