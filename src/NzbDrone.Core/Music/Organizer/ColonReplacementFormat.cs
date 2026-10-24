namespace NzbDrone.Core.Music.Organizer
{
    // Lidarr's values, stored in Music_NamingConfig. Each module has its own enum.
    public enum ColonReplacementFormat
    {
        Delete = 0,
        Dash = 1,
        SpaceDash = 2,
        SpaceDashSpace = 3,
        Smart = 4
    }
}
