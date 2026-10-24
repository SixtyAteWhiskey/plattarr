using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Music.Qualities
{
    // Resets the sizes (and optionally the titles) of the audio quality definitions to the code
    // defaults. A separate type from the video command: commands are found by type name.
    public class ResetAudioQualityDefinitionsCommand : Command
    {
        public bool ResetTitles { get; set; }

        public ResetAudioQualityDefinitionsCommand(bool resetTitles = false)
        {
            ResetTitles = resetTitles;
        }
    }
}
