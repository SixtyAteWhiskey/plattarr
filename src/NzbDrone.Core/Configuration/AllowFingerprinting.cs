namespace NzbDrone.Core.Configuration
{
    // Music: when audio files are fingerprinted (Chromaprint) to help identify them on import.
    public enum AllowFingerprinting
    {
        Never,
        NewFiles,
        AllFiles
    }
}
