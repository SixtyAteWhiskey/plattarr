using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.Search
{
    public class CutoffUnmetAlbumSearchCommand : Command
    {
        public int? ArtistId { get; set; }

        public override bool SendUpdatesToClient => true;

        public CutoffUnmetAlbumSearchCommand()
        {
        }

        public CutoffUnmetAlbumSearchCommand(int artistId)
        {
            ArtistId = artistId;
        }
    }
}
