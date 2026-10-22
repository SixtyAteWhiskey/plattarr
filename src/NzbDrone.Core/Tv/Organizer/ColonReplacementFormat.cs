namespace NzbDrone.Core.Tv.Organizer
{
    // Sonarr's values, stored in Tv_NamingConfig. The movie module has its own enum without Custom.
    public enum ColonReplacementFormat
    {
        Delete = 0,
        Dash = 1,
        SpaceDash = 2,
        SpaceDashSpace = 3,
        Smart = 4,
        Custom = 5
    }
}
