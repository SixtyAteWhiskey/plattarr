using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Tags;

namespace NzbDrone.Core.Upstream
{
    // Writes the union of every source's tags, merged by label (docs/importer.md, "Tags"), and
    // fills the IdMap that every later stage uses to translate a Tags column.
    public class TagsStage : IImportStage
    {
        private readonly ITagRepository _tags;

        public TagsStage(ITagRepository tags)
        {
            _tags = tags;
        }

        public string Name => "Tags";

        public int Order => ImportOrder.Tags;

        public IReadOnlyList<SourceTableRef> Tables { get; } = new[]
        {
            new SourceTableRef(UpstreamApps.Radarr, TagMerger.Table),
            new SourceTableRef(UpstreamApps.Prowlarr, TagMerger.Table),
            new SourceTableRef(UpstreamApps.Sonarr, TagMerger.Table)
        };

        public void Run(ImportSession session)
        {
            var plan = TagMerger.Plan(session.Snapshots);

            foreach (var skip in plan.Skips)
            {
                session.Skip(skip);
            }

            var models = plan.Tags.Select(t => new Tag { Label = t.Label }).ToList();

            _tags.InsertMany(models, session.Connection, session.Transaction);

            for (var i = 0; i < models.Count; i++)
            {
                plan.Tags[i].RecordTarget(session.IdMap, models[i].Id);

                foreach (var (app, _) in plan.Tags[i].Sources)
                {
                    session.Imported(app, TagMerger.Table);
                }
            }
        }
    }
}
