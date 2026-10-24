using NzbDrone.Core.Music.Organizer;

namespace Radarr.Api.V3.Music.Config
{
    public class MusicNamingExampleResource
    {
        public string SingleTrackExample { get; set; }
        public string MultiDiscTrackExample { get; set; }
        public string ArtistFolderExample { get; set; }
        public string AlbumFolderExample { get; set; }
    }

    public static class MusicNamingConfigResourceMapper
    {
        public static MusicNamingConfigResource ToResource(this NamingConfig model)
        {
            return new MusicNamingConfigResource
            {
                Id = model.Id,

                RenameTracks = model.RenameTracks,
                ReplaceIllegalCharacters = model.ReplaceIllegalCharacters,
                ColonReplacementFormat = (int)model.ColonReplacementFormat,
                StandardTrackFormat = model.StandardTrackFormat,
                MultiDiscTrackFormat = model.MultiDiscTrackFormat,
                ArtistFolderFormat = model.ArtistFolderFormat
            };
        }

        public static void AddToResource(this BasicNamingConfig basicNamingConfig, MusicNamingConfigResource resource)
        {
            resource.IncludeArtistName = basicNamingConfig.IncludeArtistName;
            resource.IncludeAlbumTitle = basicNamingConfig.IncludeAlbumTitle;
            resource.IncludeQuality = basicNamingConfig.IncludeQuality;
            resource.ReplaceSpaces = basicNamingConfig.ReplaceSpaces;
            resource.Separator = basicNamingConfig.Separator;
            resource.NumberStyle = basicNamingConfig.NumberStyle;
        }

        public static NamingConfig ToModel(this MusicNamingConfigResource resource)
        {
            return new NamingConfig
            {
                Id = resource.Id,

                RenameTracks = resource.RenameTracks,
                ReplaceIllegalCharacters = resource.ReplaceIllegalCharacters,
                ColonReplacementFormat = (ColonReplacementFormat)resource.ColonReplacementFormat,
                StandardTrackFormat = resource.StandardTrackFormat,
                MultiDiscTrackFormat = resource.MultiDiscTrackFormat,

                ArtistFolderFormat = resource.ArtistFolderFormat
            };
        }
    }
}
