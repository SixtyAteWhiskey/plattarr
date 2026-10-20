using NzbDrone.Core.Annotations;

namespace NzbDrone.Core.Movies.Indexers.Legacy;

public enum FailDownloads
{
    [FieldOption(Label = "Executables")]
    Executables = 0,

    [FieldOption(Label = "Potentially Dangerous")]
    PotentiallyDangerous = 1
}
