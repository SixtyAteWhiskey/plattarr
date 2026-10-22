namespace NzbDrone.Core.Upstream
{
    // One row the importer did not write, for the report. Name is shown to the
    // user, so the planner puts only a title or a provider name in it, never a
    // URL or a setting.
    public class ImportSkip
    {
        public ImportSkip(string app, string table, long? sourceId, string name, string reason, string detail)
        {
            App = app;
            Table = table;
            SourceId = sourceId;
            Name = name;
            Reason = reason;
            Detail = detail;
        }

        public string App { get; }
        public string Table { get; }
        public long? SourceId { get; }
        public string Name { get; }

        // One of the SkipReason codes.
        public string Reason { get; }
        public string Detail { get; }
    }
}
