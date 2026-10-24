using System.Collections.Generic;
using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Music
{
    public class MusicTaggedItemSource : ITaggedItemSource
    {
        private readonly IArtistService _artistService;

        public MusicTaggedItemSource(IArtistService artistService)
        {
            _artistService = artistService;
        }

        public MediaKind Kind => MediaKind.Music;

        public Dictionary<int, List<int>> AllItemTags()
        {
            return _artistService.GetAllArtistsTags();
        }
    }
}
