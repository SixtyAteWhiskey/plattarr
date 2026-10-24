using System;
using System.Collections.Generic;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Music.CustomFormats;
using NzbDrone.Core.Qualities;
using Radarr.Api.V3.CustomFormats;
using Radarr.Api.V3.Music.Artist;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Music.Blocklist
{
    public class AlbumBlocklistResource : RestResource
    {
        public int ArtistId { get; set; }
        public List<int> AlbumIds { get; set; }
        public string SourceTitle { get; set; }
        public AudioQualityModel Quality { get; set; }
        public List<CustomFormatResource> CustomFormats { get; set; }
        public DateTime Date { get; set; }
        public DownloadProtocol Protocol { get; set; }
        public string Indexer { get; set; }
        public string Message { get; set; }

        public ArtistResource Artist { get; set; }
    }

    public static class AlbumBlocklistResourceMapper
    {
        public static AlbumBlocklistResource MapToResource(this NzbDrone.Core.Music.Blocklisting.Blocklist model, IMusicCustomFormatCalculationService formatCalculator)
        {
            if (model == null)
            {
                return null;
            }

            return new AlbumBlocklistResource
            {
                Id = model.Id,

                ArtistId = model.ParentId,
                AlbumIds = model.ItemIds,
                SourceTitle = model.SourceTitle,
                Quality = model.Quality,
                CustomFormats = formatCalculator.ParseCustomFormat(model, model.Artist).ToResource(false),
                Date = model.Date,
                Protocol = model.Protocol,
                Indexer = model.Indexer,
                Message = model.Message,

                Artist = model.Artist.ToResource()
            };
        }
    }
}
