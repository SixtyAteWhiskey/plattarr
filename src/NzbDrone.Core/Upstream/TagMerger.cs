using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Upstream
{
    // One tag to write: the label as first seen, and every source tag it stands for.
    public class PlannedTag
    {
        private readonly List<(string App, long SourceId)> _sources = new List<(string App, long SourceId)>();

        public PlannedTag(string label)
        {
            Label = label;
        }

        public string Label { get; }
        public IReadOnlyList<(string App, long SourceId)> Sources => _sources;

        internal void AddSource(string app, long sourceId)
        {
            _sources.Add((app, sourceId));
        }

        // Called by the writer once the database has assigned the tag's ID.
        public void RecordTarget(IdMap idMap, long targetId)
        {
            foreach (var (app, sourceId) in _sources)
            {
                idMap.Record(app, TagMerger.Table, sourceId, targetId);
            }
        }
    }

    public class TagPlan
    {
        public TagPlan(IReadOnlyList<PlannedTag> tags, IReadOnlyList<ImportSkip> skips)
        {
            Tags = tags;
            Skips = skips;
        }

        public IReadOnlyList<PlannedTag> Tags { get; }
        public IReadOnlyList<ImportSkip> Skips { get; }
    }

    // Tags merge across sources by label, compared without case and ignoring
    // surrounding spaces. The union is written once (docs/importer.md, "Tags").
    public static class TagMerger
    {
        public const string Table = "Tags";

        public static TagPlan Plan(IEnumerable<SourceSnapshot> snapshots)
        {
            var byLabel = new Dictionary<string, PlannedTag>(StringComparer.OrdinalIgnoreCase);
            var tags = new List<PlannedTag>();
            var skips = new List<ImportSkip>();

            foreach (var snapshot in snapshots)
            {
                foreach (var row in snapshot.Rows(Table))
                {
                    var id = row.GetLong("Id");
                    var label = row.GetString("Label")?.Trim();

                    if (string.IsNullOrEmpty(label))
                    {
                        skips.Add(new ImportSkip(snapshot.AppName, Table, id, null, SkipReason.Corrupt, "Tag has no label"));
                        continue;
                    }

                    if (!byLabel.TryGetValue(label, out var planned))
                    {
                        planned = new PlannedTag(label);
                        byLabel[label] = planned;
                        tags.Add(planned);
                    }

                    planned.AddSource(snapshot.AppName, id);
                }
            }

            return new TagPlan(tags, skips);
        }
    }

    // Translates the Tags column every tag-using table carries: a JSON array of tag IDs.
    public static class TagRemapper
    {
        // A tag ID with no imported tag is a reference to a tag that no longer
        // exists upstream; it is dropped, and the count goes in the report. A
        // column that is not an array of integers is corrupt.
        public static List<int> Remap(string app, JToken tags, IdMap idMap, out int dropped)
        {
            dropped = 0;

            var result = new List<int>();

            if (tags == null || tags.Type == JTokenType.Null)
            {
                return result;
            }

            if (tags is not JArray array)
            {
                throw new CorruptSourceDataException("A Tags column does not hold a JSON array");
            }

            foreach (var item in array)
            {
                if (item.Type != JTokenType.Integer)
                {
                    throw new CorruptSourceDataException("A Tags column holds a value that is not an integer");
                }

                if (!idMap.TryResolve(app, TagMerger.Table, item.Value<long>(), out var target))
                {
                    dropped++;
                    continue;
                }

                if (!result.Contains((int)target))
                {
                    result.Add((int)target);
                }
            }

            return result;
        }
    }
}
