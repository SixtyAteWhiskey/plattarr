using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Music.Profiles
{
    public class ProfilePrimaryAlbumTypeItem : IEmbeddedDocument
    {
        public PrimaryAlbumType PrimaryAlbumType { get; set; }
        public bool Allowed { get; set; }
    }
}
