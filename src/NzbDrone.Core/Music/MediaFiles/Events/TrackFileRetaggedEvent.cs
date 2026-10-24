using System;
using System.Collections.Generic;
using NzbDrone.Common.Messaging;

namespace NzbDrone.Core.Music.MediaFiles.Events
{
    public class TrackFileRetaggedEvent : IEvent
    {
        public Artist Artist { get; private set; }
        public TrackFile TrackFile { get; private set; }
        public Dictionary<string, Tuple<string, string>> Diff { get; private set; }
        public bool Scrubbed { get; private set; }

        public TrackFileRetaggedEvent(Artist artist,
                                      TrackFile trackFile,
                                      Dictionary<string, Tuple<string, string>> diff,
                                      bool scrubbed)
        {
            Artist = artist;
            TrackFile = trackFile;
            Diff = diff;
            Scrubbed = scrubbed;
        }
    }
}
