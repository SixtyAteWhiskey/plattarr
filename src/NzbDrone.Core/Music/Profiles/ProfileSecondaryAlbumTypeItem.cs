using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Music.Profiles
{
    public class ProfileSecondaryAlbumTypeItem : IEmbeddedDocument
    {
        public SecondaryAlbumType SecondaryAlbumType { get; set; }
        public bool Allowed { get; set; }
    }
}
