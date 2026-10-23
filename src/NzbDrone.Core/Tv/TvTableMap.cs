using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Tv.Blocklisting;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.History;
using NzbDrone.Core.Tv.MediaFiles;
using NzbDrone.Core.Tv.Organizer;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Pending;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Tv
{
    public class TvTableMap : IModuleTableMap
    {
        public MediaKind Kind => MediaKind.Tv;

        public string TablePrefix => "Tv_";

        public void RegisterTypeHandlers()
        {
            // Every IEmbeddedDocument type in the assembly, and a list of it, gets its handler in
            // TableMapping (Actor, Ratings, Season, SeasonType, AddSeriesOptions, MediaInfoModel and
            // the media cover list). The pending releases table keeps the parsed episode info and
            // the match information as documents, like Movies' ParsedMovieInfo.
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<ParsedEpisodeInfo>(new QualityIntConverter(), new LanguageIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<PendingReleaseAdditionalInfo>());
        }

        public void MapTables(TableMapper mapper)
        {
            mapper.Entity<RootFolder>("Tv_RootFolders").RegisterModel()
                  .Ignore(r => r.Accessible)
                  .Ignore(r => r.IsEmpty)
                  .Ignore(r => r.FreeSpace)
                  .Ignore(r => r.TotalSpace);

            mapper.Entity<Series>("Tv_Series").RegisterModel()
                  .Ignore(s => s.RootFolderPath)
                  .Ignore(s => s.Translations)
                  .HasOne(s => s.QualityProfile, s => s.QualityProfileId);

            mapper.Entity<SeriesTranslation>("Tv_SeriesTranslations").RegisterModel();

            mapper.Entity<EpisodeFile>("Tv_EpisodeFiles").RegisterModel()
                  .HasOne(f => f.Series, f => f.SeriesId)
                  .LazyLoad(x => x.Episodes,
                            (db, parent) => db.Query<Episode>(new SqlBuilder(db.DatabaseType).Where<Episode>(c => c.EpisodeFileId == parent.Id)).ToList(),
                            t => t.Id > 0)
                  .Ignore(f => f.Path);

            mapper.Entity<Episode>("Tv_Episodes").RegisterModel()
                  .Ignore(e => e.SeriesTitle)
                  .Ignore(e => e.Series)
                  .Ignore(e => e.HasFile)
                  .Ignore(e => e.AbsoluteEpisodeNumberAdded)
                  .HasOne(e => e.EpisodeFile, e => e.EpisodeFileId);

            mapper.Entity<SceneMapping>("Tv_SceneMappings").RegisterModel();
            mapper.Entity<NamingConfig>("Tv_NamingConfig").RegisterModel();

            // The module's views of shared tables. Rows carry MediaKind; the repositories scope
            // every query to TV.
            mapper.Entity<EpisodeHistory>("History").RegisterModel();
            mapper.Entity<Blocklist>("Blocklist").RegisterModel();

            // RemoteEpisode is not persisted. It has no type handler, so RegisterModel skips it.
            mapper.Entity<PendingRelease>("PendingReleases").RegisterModel();
        }
    }
}
