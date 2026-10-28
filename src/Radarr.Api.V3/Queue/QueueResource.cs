using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Music;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Tv;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Movies;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Artist;
using Radarr.Api.V3.Tv.Episodes;
using Radarr.Api.V3.Tv.Series;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Queue
{
    public class QueueResource : RestResource
    {
        public int? MovieId { get; set; }
        public MovieResource Movie { get; set; }

        // Set for downloads matched to a series; omitted for movies so Radarr's output is unchanged.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SeriesId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EpisodeId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? SeasonNumber { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SeriesResource Series { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EpisodeResource Episode { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? EpisodeHasFile { get; set; }

        // Set for downloads matched to an artist; omitted for movies and series.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ArtistId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? AlbumId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ArtistResource Artist { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AlbumResource Album { get; set; }

        // Tracks of the album's monitored release and how many of them have a file (Lidarr's queue fields; the
        // artist views add the queued tracks to their progress). Written only for a Music download.
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TrackFileCount { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TrackHasFileCount { get; set; }
        public List<Language> Languages { get; set; }
        public QualityModel Quality { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public decimal Size { get; set; }
        public string Title { get; set; }

        // Collides with existing properties due to case-insensitive deserialization
        // public decimal SizeLeft { get; set; }
        // public TimeSpan? TimeLeft { get; set; }

        public DateTime? EstimatedCompletionTime { get; set; }
        public DateTime? Added { get; set; }
        public QueueStatus Status { get; set; }
        public TrackedDownloadStatus? TrackedDownloadStatus { get; set; }
        public TrackedDownloadState? TrackedDownloadState { get; set; }
        public List<TrackedDownloadStatusMessage> StatusMessages { get; set; }
        public string ErrorMessage { get; set; }
        public string DownloadId { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public MediaKind? MediaKind { get; set; }

        public DownloadProtocol Protocol { get; set; }
        public string DownloadClient { get; set; }
        public bool DownloadClientHasPostImportCategory { get; set; }
        public string Indexer { get; set; }
        public string OutputPath { get; set; }

        [Obsolete("Will be replaced by SizeLeft")]
        public decimal Sizeleft { get; set; }

        [Obsolete("Will be replaced by TimeLeft")]
        public TimeSpan? Timeleft { get; set; }
    }

    public static class QueueResourceMapper
    {
        public static QueueResource ToResource(this NzbDrone.Core.Queue.Queue model, bool includeMovie, bool includeSeries = false, bool includeEpisode = false, bool includeArtist = false, bool includeAlbum = false)
        {
            if (model == null)
            {
                return null;
            }

            var series = model.GetSeries();
            var episode = model.GetEpisode();
            var artist = model.GetArtist();
            var album = model.GetAlbum();
            var albumTracks = album?.AlbumReleases?.Value?.FirstOrDefault(x => x.Monitored)?.Tracks?.Value;
            var customFormats = model.RemoteRelease?.CustomFormats;
            var customFormatScore = model.GetMovie()?.QualityProfile?.CalculateCustomFormatScore(customFormats)
                                    ?? series?.QualityProfile?.Value?.CalculateCustomFormatScore(customFormats)
                                    ?? artist?.QualityProfile?.Value?.CalculateCustomFormatScore(customFormats)
                                    ?? 0;

            return new QueueResource
            {
                Id = model.Id,
                MovieId = model.GetMovie()?.Id,
                Movie = includeMovie && model.GetMovie() != null ? model.GetMovie().ToResource(0) : null,
                SeriesId = series?.Id,
                EpisodeId = episode?.Id,
                SeasonNumber = episode?.SeasonNumber,
                Series = includeSeries && series != null ? series.ToResource() : null,
                Episode = includeEpisode && episode != null ? episode.ToResource() : null,
                EpisodeHasFile = episode?.HasFile,
                ArtistId = artist?.Id,
                AlbumId = album?.Id,
                Artist = includeArtist && artist != null ? artist.ToResource() : null,
                Album = includeAlbum && album != null ? album.ToResource() : null,
                TrackFileCount = album == null ? null : albumTracks?.Count ?? 0,
                TrackHasFileCount = album == null ? null : albumTracks?.Count(x => x.HasFile) ?? 0,
                Languages = model.Languages,
                Quality = model.Quality,
                CustomFormats = customFormats?.ToResource(false),
                CustomFormatScore = customFormatScore,
                Size = model.Size,
                Title = model.Title,

                // Collides with existing properties due to case-insensitive deserialization
                // SizeLeft = model.SizeLeft,
                // TimeLeft = model.TimeLeft,

                EstimatedCompletionTime = model.EstimatedCompletionTime,
                Added = model.Added,
                Status = model.Status,
                TrackedDownloadStatus = model.TrackedDownloadStatus,
                TrackedDownloadState = model.TrackedDownloadState,
                StatusMessages = model.StatusMessages,
                ErrorMessage = model.ErrorMessage,
                DownloadId = model.DownloadId,
                MediaKind = model.Kind ?? model.RemoteRelease?.Kind,
                Protocol = model.Protocol,
                DownloadClient = model.DownloadClient,
                DownloadClientHasPostImportCategory = model.DownloadClientHasPostImportCategory,
                Indexer = model.Indexer,
                OutputPath = model.OutputPath,

#pragma warning disable CS0618
                Sizeleft = model.SizeLeft,
                Timeleft = model.TimeLeft,
#pragma warning restore CS0618
            };
        }

        public static List<QueueResource> ToResource(this IEnumerable<NzbDrone.Core.Queue.Queue> models, bool includeMovie, bool includeSeries = false, bool includeEpisode = false, bool includeArtist = false, bool includeAlbum = false)
        {
            return models.Select((m) => ToResource(m, includeMovie, includeSeries, includeEpisode, includeArtist, includeAlbum)).ToList();
        }
    }
}
