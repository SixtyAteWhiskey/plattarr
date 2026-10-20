using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.IndexerSearch
{
    public interface IIndexerCategoryService
    {
        // The categories a media type searches and receives on one indexer: the indexer's
        // override for that media type when set, otherwise the media type's defaults.
        int[] GetCategories(int indexerId, IReleaseMatcher matcher);

        // Whether an RSS release belongs to a media type (docs/module-interface.md, contract 2).
        bool BelongsTo(ReleaseInfo release, IReleaseMatcher matcher);
    }

    public class IndexerCategoryService : IIndexerCategoryService
    {
        // Prowlarr numbers indexer-specific categories from 100000. They carry no standard
        // meaning unless an override names them.
        public const int CustomCategoryStart = 100000;

        private readonly IIndexerOverrideService _indexerOverrideService;

        public IndexerCategoryService(IIndexerOverrideService indexerOverrideService)
        {
            _indexerOverrideService = indexerOverrideService;
        }

        public int[] GetCategories(int indexerId, IReleaseMatcher matcher)
        {
            var overridden = _indexerOverrideService.Get(indexerId, matcher.Kind).Categories;

            return overridden.Count > 0 ? overridden.ToArray() : matcher.Categories.ToArray();
        }

        public bool BelongsTo(ReleaseInfo release, IReleaseMatcher matcher)
        {
            return Matches(release, GetCategories(release.IndexerId, matcher));
        }

        // A release matches when one of its categories equals a wanted category or falls in
        // a wanted top-level category's range (2000 covers 2000 to 2999). A release with no
        // categories, or only indexer-specific ones nobody asked for, is offered to every
        // media type, which then parses the title.
        public static bool Matches(ReleaseInfo release, IReadOnlyCollection<int> categories)
        {
            var ids = release.Categories?.Select(c => c.Id).ToList();

            if (ids == null || ids.Count == 0)
            {
                return true;
            }

            if (ids.Any(id => categories.Any(c => Contains(c, id))))
            {
                return true;
            }

            return ids.All(id => id >= CustomCategoryStart);
        }

        private static bool Contains(int category, int id)
        {
            if (id == category)
            {
                return true;
            }

            return category < CustomCategoryStart && category % 1000 == 0 && id >= category && id < category + 1000;
        }
    }
}
