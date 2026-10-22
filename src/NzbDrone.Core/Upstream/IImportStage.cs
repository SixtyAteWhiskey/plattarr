using System.Collections.Generic;

namespace NzbDrone.Core.Upstream
{
    // Source app names as the adapters report them (IUpstreamAdapter.AppName).
    public static class UpstreamApps
    {
        public const string Radarr = "Radarr";
        public const string Prowlarr = "Prowlarr";
    }

    // Where each stage runs. Dependencies decide the order (docs/importer.md, "Write
    // order"); the gaps leave room for a stage a later app or module needs between two of
    // these. A stage that reads the IdMap runs after the stages that fill it.
    public static class ImportOrder
    {
        public const int Tags = 100;
        public const int RootFolders = 500;
    }

    // One source table of one app.
    public readonly record struct SourceTableRef(string App, string Table);

    // One step of the import. It plans target rows for some source tables from what earlier
    // stages put in the IdMap, writes them on the session's connection and transaction (the
    // repositories' caller-transaction overloads, never a plain InsertMany, which commits at
    // once), and records the new IDs. Stages live where their vocabulary lives: a stage that
    // knows movie tables is Movies module code, one that does not is core.
    public interface IImportStage
    {
        string Name { get; }

        // Lower runs first.
        int Order { get; }

        // The source tables this stage accounts for. Every row of each ends up imported or
        // skipped; the reconciliation fails the run when the counts do not add up.
        IReadOnlyList<SourceTableRef> Tables { get; }

        void Run(ImportSession session);
    }
}
