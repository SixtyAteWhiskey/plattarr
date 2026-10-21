using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    // Porting step 9f (docs/module-interface.md contract 6): the shared activity
    // tables stop naming a media type in their columns. The first migration since
    // 001_initial, so it upgrades a database in place.
    //
    // - History, DownloadHistory, Blocklist, and PendingReleases: MovieId becomes
    //   ParentId (the Movie, Series, or Artist the row belongs to). For Movies,
    //   parent and item are the same entity, so no row changes value.
    // - PendingReleases: ParsedMovieInfo becomes ParsedInfo. The column holds the
    //   owning module's parsed release as JSON; the module that wrote the row reads
    //   it back.
    // - The three MovieId indexes are replaced by ParentId ones.
    //
    // No ItemId or ItemIds columns yet: nothing reads them until a module has more
    // than one item per parent (spec, contract 6 as-built note).
    [Migration(2)]
    public class generalize_activity_tables : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            Delete.Index("IX_History_MediaKind_MovieId_Date").OnTable("History");
            Delete.Index("IX_DownloadHistory_MediaKind_MovieId").OnTable("DownloadHistory");
            Delete.Index("IX_Blocklist_MediaKind_MovieId").OnTable("Blocklist");

            Rename.Column("MovieId").OnTable("History").To("ParentId");
            Rename.Column("MovieId").OnTable("DownloadHistory").To("ParentId");
            Rename.Column("MovieId").OnTable("Blocklist").To("ParentId");
            Rename.Column("MovieId").OnTable("PendingReleases").To("ParentId");
            Rename.Column("ParsedMovieInfo").OnTable("PendingReleases").To("ParsedInfo");

            Create.Index("IX_History_MediaKind_ParentId_Date").OnTable("History").OnColumn("MediaKind").Ascending().OnColumn("ParentId").Ascending().OnColumn("Date").Descending();
            Create.Index("IX_DownloadHistory_MediaKind_ParentId").OnTable("DownloadHistory").OnColumn("MediaKind").Ascending().OnColumn("ParentId").Ascending();
            Create.Index("IX_Blocklist_MediaKind_ParentId").OnTable("Blocklist").OnColumn("MediaKind").Ascending().OnColumn("ParentId").Ascending();
        }
    }
}
