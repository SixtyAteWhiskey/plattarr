using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.Music.Search
{
    // Lidarr's AlbumSearchCriteria: a search for one album of an artist.
    public class AlbumSearchRequest : MusicSearchRequest
    {
        public string AlbumTitle { get; set; }
        public int AlbumYear { get; set; }
        public string Disambiguation { get; set; }

        public string AlbumQuery => $"{AlbumTitle}{(Disambiguation.IsNullOrWhiteSpace() ? string.Empty : $"+{Disambiguation}")}";
        public string CleanAlbumQuery => GetQueryTitle(AlbumQuery);

        public override string ToString()
        {
            return $"[{Artist?.Name} - {AlbumTitle}{(Disambiguation.IsNullOrWhiteSpace() ? string.Empty : $" ({Disambiguation})")} ({AlbumYear})]";
        }
    }
}
