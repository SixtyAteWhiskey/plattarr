using System;
using System.Collections.Generic;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Music.History;
using NzbDrone.Core.Qualities;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Music.Albums;
using Radarr.Api.V3.Music.Tracks;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.History
{
    // Lidarr's HistoryResource on the Music history rows. The track a file event is about is the
    // "trackId" entry of Data (the shared History table has no column for it).
    public class AlbumHistoryResource : RestResource
    {
        public int AlbumId { get; set; }
        public int ArtistId { get; set; }
        public int TrackId { get; set; }
        public string SourceTitle { get; set; }
        public AudioQualityModel Quality { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public int CustomFormatScore { get; set; }
        public bool QualityCutoffNotMet { get; set; }
        public DateTime Date { get; set; }
        public string DownloadId { get; set; }

        public AlbumHistoryEventType EventType { get; set; }

        public Dictionary<string, string> Data { get; set; }

        public AlbumResource Album { get; set; }
        public Artist.ArtistResource Artist { get; set; }
        public TrackResource Track { get; set; }
    }

    public static class AlbumHistoryResourceMapper
    {
        public static AlbumHistoryResource ToResource(this AlbumHistory model, IMusicCustomFormatCalculationService formatCalculator)
        {
            if (model == null)
            {
                return null;
            }

            var customFormats = formatCalculator.ParseCustomFormat(model, model.Artist);
            var customFormatScore = model.Artist?.QualityProfile?.Value?.CalculateCustomFormatScore(customFormats) ?? 0;

            return new AlbumHistoryResource
            {
                Id = model.Id,

                AlbumId = model.ItemId,
                ArtistId = model.ParentId,
                TrackId = model.TrackId(),
                SourceTitle = model.SourceTitle,
                Quality = model.Quality,
                CustomFormats = customFormats.ToResource(false),
                CustomFormatScore = customFormatScore,

                // QualityCutoffNotMet
                Date = model.Date,
                DownloadId = model.DownloadId,

                EventType = model.EventType,

                Data = model.Data

                // Album
                // Artist
                // Track
            };
        }

        // The id of the track a file event is about, 0 for the rows that are about an album.
        public static int TrackId(this AlbumHistory model)
        {
            return model.Data != null &&
                   model.Data.TryGetValue(AlbumHistory.TRACK_ID, out var value) &&
                   int.TryParse(value, out var trackId)
                ? trackId
                : 0;
        }
    }
}
