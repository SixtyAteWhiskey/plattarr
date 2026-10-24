using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Music.Profiles
{
    public class ProfileReleaseStatusItem : IEmbeddedDocument
    {
        public ReleaseStatus ReleaseStatus { get; set; }
        public bool Allowed { get; set; }
    }
}
