using System.Collections.Generic;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Movies.AutoTagging;
using NzbDrone.Core.Movies.AutoTagging.Specifications;
using NzbDrone.Core.Movies.Blocklisting;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Extras.Metadata;
using NzbDrone.Core.Movies.Extras.Metadata.Files;
using NzbDrone.Core.Movies.Extras.Others;
using NzbDrone.Core.Movies.Extras.Subtitles;
using NzbDrone.Core.Movies.History;
using NzbDrone.Core.Movies.ImportLists;
using NzbDrone.Core.Movies.ImportLists.ImportExclusions;
using NzbDrone.Core.Movies.ImportLists.ImportListMovies;
using NzbDrone.Core.Movies.MediaFiles;
using NzbDrone.Core.Movies.Organizer;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Movies.Pending;
using NzbDrone.Core.Movies.RootFolders;
using NzbDrone.Core.Movies.Translations;

namespace NzbDrone.Core.Movies
{
    public class MoviesTableMap : IModuleTableMap
    {
        public MediaKind Kind => MediaKind.Movies;

        public string TablePrefix => "Movies_";

        public void RegisterTypeHandlers()
        {
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<IAutoTaggingSpecification>>(new AutoTaggingSpecificationConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<ParsedMovieInfo>(new QualityIntConverter(), new LanguageIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<Ratings>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<PendingReleaseAdditionalInfo>());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<MovieTranslation>>());
        }

        public void MapTables(TableMapper mapper)
        {
            mapper.Entity<RootFolder>("Movies_RootFolders").RegisterModel()
                  .Ignore(r => r.Accessible)
                  .Ignore(r => r.FreeSpace)
                  .Ignore(r => r.TotalSpace);

            mapper.Entity<ImportListDefinition>("Movies_ImportLists").RegisterModel()
                  .Ignore(x => x.ImplementationName)
                  .Ignore(i => i.ListType)
                  .Ignore(i => i.MinRefreshInterval)
                  .Ignore(i => i.Enable);

            mapper.Entity<MetadataDefinition>("Movies_Metadata").RegisterModel()
                  .Ignore(x => x.ImplementationName)
                  .Ignore(d => d.Tags);

            mapper.Entity<MovieFile>("Movies_MovieFiles").RegisterModel()
                  .Ignore(f => f.Path);

            mapper.Entity<Movie>("Movies_Movies").RegisterModel()
                  .Ignore(s => s.RootFolderPath)
                  .Ignore(s => s.Title)
                  .Ignore(s => s.Year)
                  .Ignore(s => s.TmdbId)
                  .Ignore(s => s.ImdbId)
                  .HasOne(a => a.MovieMetadata, a => a.MovieMetadataId);

            mapper.Entity<ImportListMovie>("Movies_ImportListMovies").RegisterModel()
                  .Ignore(s => s.Title)
                  .Ignore(s => s.Year)
                  .Ignore(s => s.TmdbId)
                  .Ignore(s => s.ImdbId)
                  .HasOne(a => a.MovieMetadata, a => a.MovieMetadataId);

            mapper.Entity<AlternativeTitle>("Movies_AlternativeTitles").RegisterModel();
            mapper.Entity<MovieTranslation>("Movies_MovieTranslations").RegisterModel();
            mapper.Entity<Credit>("Movies_Credits").RegisterModel();
            mapper.Entity<NamingConfig>("Movies_NamingConfig").RegisterModel();
            mapper.Entity<MetadataFile>("Movies_MetadataFiles").RegisterModel();
            mapper.Entity<SubtitleFile>("Movies_SubtitleFiles").RegisterModel();
            mapper.Entity<OtherExtraFile>("Movies_ExtraFiles").RegisterModel();
            mapper.Entity<ImportListStatus>("Movies_ImportListStatus").RegisterModel();

            mapper.Entity<MovieMetadata>("Movies_MovieMetadata").RegisterModel()
                  .Ignore(s => s.Translations);

            mapper.Entity<MovieCollection>("Movies_Collections").RegisterModel();

            // The module's views of shared tables. Rows carry MediaKind; the repositories scope
            // every query to movies.
            mapper.Entity<MovieHistory>("History").RegisterModel();
            mapper.Entity<Blocklist>("Blocklist").RegisterModel();

            // RemoteMovie is not persisted. It has no type handler, so RegisterModel skips it.
            mapper.Entity<PendingRelease>("PendingReleases").RegisterModel();

            // Global tables whose entity code is still Radarr's (spec, "Other tables").
            mapper.Entity<ImportListExclusion>("ImportExclusions").RegisterModel();
            mapper.Entity<AutoTag>("AutoTagging").RegisterModel();
        }
    }
}
