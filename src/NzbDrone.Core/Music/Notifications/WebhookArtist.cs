using System.Collections.Generic;
using System.Linq;

namespace NzbDrone.Core.Music.Notifications
{
    // Lidarr's webhook artist, field for field, so receivers written for Lidarr keep working.
    public class WebhookArtist
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Disambiguation { get; set; }
        public string Path { get; set; }
        public string MBId { get; set; }
        public string Type { get; set; }
        public string Overview { get; set; }
        public List<string> Genres { get; set; }
        public List<WebhookImage> Images { get; set; }
        public List<string> Tags { get; set; }

        public WebhookArtist()
        {
        }

        public WebhookArtist(Artist artist, List<string> tags)
        {
            Id = artist.Id;
            Name = artist.Metadata.Value.Name;
            Disambiguation = artist.Metadata.Value.Disambiguation;
            Path = artist.Path;
            MBId = artist.Metadata.Value.ForeignArtistId;
            Type = artist.Metadata.Value.Type;
            Overview = artist.Metadata.Value.Overview;
            Genres = artist.Metadata.Value.Genres;
            Images = artist.Metadata.Value.Images.Select(i => new WebhookImage(i)).ToList();
            Tags = tags;
        }
    }
}
