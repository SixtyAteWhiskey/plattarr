using NzbDrone.Core.Languages;

namespace Radarr.Api.V3.Tv.Series
{
    public class SeriesFolderResource
    {
        public Language Language { get; set; }
        public string Folder { get; set; }
    }
}
