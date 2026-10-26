namespace NzbDrone.Core.Music.MediaFiles.TrackImport;

// Why an audio file, an album release or a download folder was not imported. Stored nowhere as an
// integer; the API shows the name. Appended to, never reordered.
public enum ImportRejectionReason
{
    Unknown,
    FileLocked,
    UnknownArtist,
    ExecutableFile,
    ArchiveFile,
    InvalidFilePath,
    ArtistFolder,
    UnsupportedExtension,
    UnableToParse,
    Error,
    DecisionError,
    MultipleVersions,
    UnmatchedAlbum,
    UnmatchedTrack,
    UnmatchedFile,
    AlbumAlreadyImported,
    AlbumMatchNotClose,
    TrackMatchNotClose,
    NoTracksMatched,
    WorstTrackMatch,
    UnmatchedTracks,
    MissingTracks,
    FewerTracksThanExisting,
    ExistingFileHasMoreTracks,
    AlbumNotRequested,
    Unpacking,
    MinimumFreeSpace,
    NotQualityUpgrade,
    NotAlbumUpgrade,
    ArtistPathNotInRootFolder,
    FailedToAddArtist,
    FailedToAddAlbum,
    DestinationAlreadyExists
}
