namespace NzbDrone.Core.Music.Covers
{
    // Which Music entity a cover belongs to. Artists and albums keep their covers in separate folders.
    public enum MediaCoverEntity
    {
        Artist = 0,
        Album = 1
    }
}
