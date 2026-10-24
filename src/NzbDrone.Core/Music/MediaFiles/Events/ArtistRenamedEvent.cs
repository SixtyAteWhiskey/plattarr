using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class ArtistRenamedEvent : IEvent
    {
        public Artist Artist { get; private set; }
        public List<RenamedTrackFile> RenamedFiles { get; private set; }

        public ArtistRenamedEvent(Artist artist, List<RenamedTrackFile> renamedFiles)
        {
            Artist = artist;
            RenamedFiles = renamedFiles;
        }
    }
}
