using System;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Trakt;
using NzbDrone.Core.Notifications.Trakt.Resource;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Movies.Notifications
{
    // Maps an imported movie file to a Trakt collection entry.
    public class MovieTraktCollectionBuilder : ITraktCollectionBuilder
    {
        public MediaKind Kind => MediaKind.Movies;

        public TraktCollectMovie BuildCollected(DownloadMessage message)
        {
            var data = (MovieNotificationData)message.Data;
            var movie = data.Movie;
            var movieFile = data.MovieFile;

            return new TraktCollectMovie
            {
                Title = movie.Title,
                Year = movie.Year,
                CollectedAt = DateTime.Now,
                Resolution = MapResolution(movieFile.Quality.Quality.Resolution, movieFile.MediaInfo?.ScanType),
                Hdr = MapHdr(movieFile),
                MediaType = MapMediaType(movieFile.Quality.Quality.Source),
                AudioChannels = MapAudioChannels(movieFile),
                Audio = MapAudio(movieFile),
                Is3D = movieFile.MediaInfo?.VideoMultiViewCount > 1,
                Ids = new TraktMovieIdsResource
                {
                    Tmdb = movie.MovieMetadata.Value.TmdbId,
                    Imdb = movie.MovieMetadata.Value.ImdbId ?? "",
                }
            };
        }

        private static string MapMediaType(QualitySource source)
        {
            var traktSource = source switch
            {
                QualitySource.BLURAY => "bluray",
                QualitySource.WEBDL => "digital",
                QualitySource.WEBRIP => "digital",
                QualitySource.DVD => "dvd",
                QualitySource.TV => "dvd",
                _ => string.Empty
            };

            return traktSource;
        }

        private static string MapResolution(int resolution, string scanType)
        {
            var scanIdentifier = scanType.IsNotNullOrWhiteSpace() && TraktInterlacedTypes.InterlacedTypes.Contains(scanType) ? "i" : "p";

            var traktResolution = resolution switch
            {
                2160 => "uhd_4k",
                1080 => $"hd_1080{scanIdentifier}",
                720 => "hd_720p",
                576 => $"sd_576{scanIdentifier}",
                480 => $"sd_480{scanIdentifier}",
                _ => string.Empty
            };

            return traktResolution;
        }

        private static string MapHdr(MovieFile movieFile)
        {
            var traktHdr = movieFile.MediaInfo?.VideoHdrFormat switch
            {
                HdrFormat.DolbyVision or HdrFormat.DolbyVisionSdr => "dolby_vision",
                HdrFormat.Hdr10 or HdrFormat.DolbyVisionHdr10 => "hdr10",
                HdrFormat.Hdr10Plus or HdrFormat.DolbyVisionHdr10Plus => "hdr10_plus",
                HdrFormat.Hlg10 or HdrFormat.DolbyVisionHlg => "hlg",
                _ => null
            };

            return traktHdr;
        }

        private static string MapAudio(MovieFile movieFile)
        {
            var audioCodec = movieFile.MediaInfo != null ? MediaInfoFormatter.FormatAudioCodec(movieFile.MediaInfo, movieFile.SceneName) : string.Empty;

            var traktAudioFormat = audioCodec switch
            {
                "AC3" => "dolby_digital",
                "EAC3" => "dolby_digital_plus",
                "TrueHD" => "dolby_truehd",
                "EAC3 Atmos" => "dolby_digital_plus_atmos",
                "TrueHD Atmos" => "dolby_atmos",
                "DTS" => "dts",
                "DTS-ES" => "dts",
                "DTS-HD MA" => "dts_ma",
                "DTS-HD HRA" => "dts_hr",
                "DTS-X" => "dts_x",
                "MP3" => "mp3",
                "MP2" => "mp2",
                "Vorbis" => "ogg",
                "WMA" => "wma",
                "AAC" => "aac",
                "PCM" => "lpcm",
                "FLAC" => "flac",
                "Opus" => "ogg_opus",
                _ => string.Empty
            };

            return traktAudioFormat;
        }

        private static string MapAudioChannels(MovieFile movieFile)
        {
            var audioChannels = movieFile.MediaInfo != null ? MediaInfoFormatter.FormatAudioChannels(movieFile.MediaInfo).ToString("0.0") : string.Empty;

            if (audioChannels == "0.0")
            {
                audioChannels = string.Empty;
            }

            return audioChannels;
        }
    }
}
