namespace NzbDrone.Core.Movies.ImportLists
{
    public enum CleanLibraryLevel
    {
        Disabled,
        LogOnly,
        KeepAndUnmonitor,
        RemoveAndKeep,
        RemoveAndDelete
    }
}
