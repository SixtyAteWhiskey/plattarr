namespace NzbDrone.Core.Configuration
{
    // Music: which audio files get their tags rewritten from the library metadata.
    public enum WriteAudioTagsType
    {
        No,
        NewFiles,
        AllFiles,
        Sync
    }
}
