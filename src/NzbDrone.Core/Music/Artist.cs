using System;
using System.Collections.Generic;
using Equ;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Music.Profiles;

namespace NzbDrone.Core.Music
{
    public class Artist : Entity<Artist>, IMediaParent
    {
        public Artist()
        {
            Tags = new HashSet<int>();
            Metadata = new ArtistMetadata();
        }

        // What the shared core reads of a parent entity (the artist is Music's parent, as a movie or series is).
        // Equ compares non-public members too, so both are ignored: Title reads the lazily loaded
        // metadata and would make two otherwise equal artists differ by whether it is loaded.
        [MemberwiseEqualityIgnore]
        MediaKind IMediaParent.Kind => MediaKind.Music;

        [MemberwiseEqualityIgnore]
        string IMediaParent.Title => Name;

        // These correspond to columns in the Artists table
        public int ArtistMetadataId { get; set; }
        public string CleanName { get; set; }
        public string SortName { get; set; }
        public bool Monitored { get; set; }
        public NewItemMonitorTypes MonitorNewItems { get; set; }
        public DateTime? LastInfoSync { get; set; }
        public string Path { get; set; }
        public string RootFolderPath { get; set; }
        public DateTime Added { get; set; }
        public int QualityProfileId { get; set; }
        public int MetadataProfileId { get; set; }
        public HashSet<int> Tags { get; set; }
        [MemberwiseEqualityIgnore]
        public AddArtistOptions AddOptions { get; set; }

        // Dynamically loaded from DB
        [MemberwiseEqualityIgnore]
        public LazyLoaded<ArtistMetadata> Metadata { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<AudioQualityProfile> QualityProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<MetadataProfile> MetadataProfile { get; set; }
        [MemberwiseEqualityIgnore]
        public LazyLoaded<List<Album>> Albums { get; set; }

        // compatibility properties
        [MemberwiseEqualityIgnore]
        public string Name
        {
            get { return Metadata.Value.Name; } set { Metadata.Value.Name = value; }
        }

        [MemberwiseEqualityIgnore]
        public string ForeignArtistId
        {
            get { return Metadata.Value.ForeignArtistId; } set { Metadata.Value.ForeignArtistId = value; }
        }

        public override string ToString()
        {
            return string.Format("[{0}][{1}]", Metadata.Value.ForeignArtistId.NullSafe(), Metadata.Value.Name.NullSafe());
        }

        public override void UseMetadataFrom(Artist other)
        {
            CleanName = other.CleanName;
            SortName = other.SortName;
        }

        public override void UseDbFieldsFrom(Artist other)
        {
            Id = other.Id;
            ArtistMetadataId = other.ArtistMetadataId;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
            LastInfoSync = other.LastInfoSync;
            Path = other.Path;
            RootFolderPath = other.RootFolderPath;
            Added = other.Added;
            QualityProfileId = other.QualityProfileId;
            MetadataProfileId = other.MetadataProfileId;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
        }

        public override void ApplyChanges(Artist other)
        {
            Path = other.Path;
            QualityProfileId = other.QualityProfileId;
            QualityProfile = other.QualityProfile;
            MetadataProfileId = other.MetadataProfileId;
            MetadataProfile = other.MetadataProfile;

            Albums = other.Albums;
            Tags = other.Tags;
            AddOptions = other.AddOptions;
            RootFolderPath = other.RootFolderPath;
            Monitored = other.Monitored;
            MonitorNewItems = other.MonitorNewItems;
        }
    }
}
