namespace NzbDrone.Core.Modules
{
    // Contract 5. Moved from Radarr's NzbDrone.Core.MediaFiles.MovieImport; values unchanged.
    public enum ImportResultType
    {
        Imported,
        Rejected,
        Skipped
    }
}
