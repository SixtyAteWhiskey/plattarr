using System.Collections.Generic;

namespace NzbDrone.Core.Music.MediaFiles
{
    public interface IAudioTagService
    {
        void SyncTags(List<Track> tracks);
    }

    // Lidarr writes audio tags when WriteAudioTags is Sync (default No). Reading and writing tags is
    // the import step (step 5); there is no setting to turn tag writing on yet, so a refresh has no
    // tags to sync. Step 5 replaces this class with Lidarr's AudioTagService.
    public class AudioTagService : IAudioTagService
    {
        public void SyncTags(List<Track> tracks)
        {
        }
    }
}
