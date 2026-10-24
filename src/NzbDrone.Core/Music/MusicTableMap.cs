using System.Collections.Generic;
using System.Linq;
using Dapper;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Blocklisting;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music
{
    public class MusicTableMap : IModuleTableMap
    {
        public MediaKind Kind => MediaKind.Music;

        public string TablePrefix => "Music_";

        public void RegisterTypeHandlers()
        {
            // Every IEmbeddedDocument type in the assembly, and a list of it, already has its
            // handler from TableMapping. These replace the plain ones where an ID or an enum-like
            // class must be stored as its number.
            SqlMapper.AddTypeHandler(new DapperAudioQualityIntConverter());
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<AudioQualityModel>(new AudioQualityIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<AudioQualityProfileItem>>(new AudioQualityIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<ProfilePrimaryAlbumTypeItem>>(new PrimaryAlbumTypeIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<ProfileSecondaryAlbumTypeItem>>(new SecondaryAlbumTypeIntConverter()));
            SqlMapper.AddTypeHandler(new EmbeddedDocumentConverter<List<ProfileReleaseStatusItem>>(new ReleaseStatusIntConverter()));
        }

        public void MapTables(TableMapper mapper)
        {
            mapper.Entity<RootFolder>("Music_RootFolders").RegisterModel()
                  .Ignore(r => r.Accessible)
                  .Ignore(r => r.FreeSpace)
                  .Ignore(r => r.TotalSpace);

            mapper.Entity<Artist>("Music_Artists")
                  .Ignore(s => s.RootFolderPath)
                  .Ignore(s => s.Name)
                  .Ignore(s => s.ForeignArtistId)
                  .HasOne(a => a.Metadata, a => a.ArtistMetadataId)
                  .HasOne(a => a.QualityProfile, a => a.QualityProfileId)
                  .HasOne(s => s.MetadataProfile, s => s.MetadataProfileId)
                  .LazyLoad(a => a.Albums, (db, a) => db.Query<Album>(new SqlBuilder(db.DatabaseType).Where<Album>(rg => rg.ArtistMetadataId == a.Id)).ToList(), a => a.Id > 0);

            mapper.Entity<ArtistMetadata>("Music_ArtistMetadata").RegisterModel();

            mapper.Entity<Album>("Music_Albums").RegisterModel()
                  .Ignore(a => a.ArtistId)
                  .HasOne(r => r.ArtistMetadata, r => r.ArtistMetadataId)
                  .LazyLoad(a => a.AlbumReleases, (db, album) => db.Query<AlbumRelease>(new SqlBuilder(db.DatabaseType).Where<AlbumRelease>(r => r.AlbumId == album.Id)).ToList(), a => a.Id > 0)
                  .LazyLoad(a => a.Artist,
                            (db, album) => ArtistRepository.Query(db, new SqlBuilder(db.DatabaseType)
                                                                 .Join<Artist, ArtistMetadata>((a, m) => a.ArtistMetadataId == m.Id)
                                                                 .Where<Artist>(a => a.ArtistMetadataId == album.ArtistMetadataId)).SingleOrDefault(),
                            a => a.ArtistMetadataId > 0);

            mapper.Entity<AlbumRelease>("Music_AlbumReleases").RegisterModel()
                  .HasOne(r => r.Album, r => r.AlbumId)
                  .LazyLoad(x => x.Tracks, (db, release) => db.Query<Track>(new SqlBuilder(db.DatabaseType).Where<Track>(t => t.AlbumReleaseId == release.Id)).ToList(), r => r.Id > 0);

            mapper.Entity<Track>("Music_Tracks").RegisterModel()
                  .Ignore(t => t.HasFile)
                  .Ignore(t => t.AlbumId)
                  .HasOne(track => track.AlbumRelease, track => track.AlbumReleaseId)
                  .HasOne(track => track.ArtistMetadata, track => track.ArtistMetadataId)
                  .LazyLoad(t => t.TrackFile,
                            (db, track) => TrackFileRepository.Query(db, new SqlBuilder(db.DatabaseType)
                                                                     .Join<TrackFile, Track>((l, r) => l.Id == r.TrackFileId)
                                                                     .Join<TrackFile, Album>((l, r) => l.AlbumId == r.Id)
                                                                     .Join<Album, Artist>((l, r) => l.ArtistMetadataId == r.ArtistMetadataId)
                                                                     .Join<Artist, ArtistMetadata>((l, r) => l.ArtistMetadataId == r.Id)
                                                                     .Where<TrackFile>(t => t.Id == track.TrackFileId)).SingleOrDefault(),
                            t => t.TrackFileId > 0)
                  .LazyLoad(x => x.Artist,
                            (db, t) => ArtistRepository.Query(db, new SqlBuilder(db.DatabaseType)
                                                                     .Join<Artist, ArtistMetadata>((a, m) => a.ArtistMetadataId == m.Id)
                                                                     .Join<Artist, Album>((l, r) => l.ArtistMetadataId == r.ArtistMetadataId)
                                                                     .Join<Album, AlbumRelease>((l, r) => l.Id == r.AlbumId)
                                                                     .Where<AlbumRelease>(r => r.Id == t.AlbumReleaseId)).SingleOrDefault(),
                            t => t.Id > 0);

            mapper.Entity<TrackFile>("Music_TrackFiles").RegisterModel()
                  .HasOne(f => f.Album, f => f.AlbumId)
                  .LazyLoad(x => x.Tracks, (db, file) => db.Query<Track>(new SqlBuilder(db.DatabaseType).Where<Track>(t => t.TrackFileId == file.Id)).ToList(), x => x.Id > 0)
                  .LazyLoad(x => x.Artist,
                            (db, f) => ArtistRepository.Query(db, new SqlBuilder(db.DatabaseType)
                                                                     .Join<Artist, ArtistMetadata>((a, m) => a.ArtistMetadataId == m.Id)
                                                                     .Join<Artist, Album>((l, r) => l.ArtistMetadataId == r.ArtistMetadataId)
                                                                     .Where<Album>(a => a.Id == f.AlbumId)).SingleOrDefault(),
                            t => t.Id > 0);

            mapper.Entity<AudioQualityDefinition>("Music_QualityDefinitions").RegisterModel()
                  .Ignore(d => d.GroupName)
                  .Ignore(d => d.GroupWeight)
                  .Ignore(d => d.Weight);

            mapper.Entity<AudioQualityProfile>("Music_QualityProfiles").RegisterModel();
            mapper.Entity<MetadataProfile>("Music_MetadataProfiles").RegisterModel();
            mapper.Entity<NamingConfig>("Music_NamingConfig").RegisterModel();

            // The module's views of shared tables. Rows carry MediaKind; the repositories scope
            // every query to Music.
            mapper.Entity<AlbumHistory>("History").RegisterModel();
            mapper.Entity<Blocklist>("Blocklist").RegisterModel();
        }
    }
}
