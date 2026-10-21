using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Notifications
{
    // One media file as notification providers see it: a Movie's file today, an episode's or a
    // track's later. The module fills it; providers read only these fields.
    public class NotificationFile
    {
        public int Id { get; set; }
        public string Path { get; set; }
        public string RelativePath { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }
        public string SceneName { get; set; }
        public string ReleaseGroup { get; set; }
        public string QualityName { get; set; }
        public NotificationMediaInfo MediaInfo { get; set; }
    }

    // Codec and language values formatted the way the UI shows them. Null on a file the scan has not read.
    public class NotificationMediaInfo
    {
        public string VideoCodec { get; set; }
        public string AudioCodec { get; set; }
        public decimal AudioChannels { get; set; }
        public List<string> AudioLanguages { get; set; } = new();
        public List<string> Subtitles { get; set; } = new();
    }

    public class NotificationRenamedFile
    {
        public NotificationFile File { get; set; }
        public string PreviousPath { get; set; }
        public string PreviousRelativePath { get; set; }
    }

    // The release a grab, import or blocked download came from.
    public class NotificationRelease
    {
        public string Title { get; set; }
        public string Indexer { get; set; }
        public long Size { get; set; }
        public string ReleaseGroup { get; set; }
        public string QualityName { get; set; }
        public List<string> CustomFormats { get; set; } = new();
        public int CustomFormatScore { get; set; }
    }
}
