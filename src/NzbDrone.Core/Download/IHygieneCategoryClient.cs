using NzbDrone.Core.Modules;

namespace NzbDrone.Core.Download
{
    // Optional client capability used for an explicit, journaled category change.
    public interface IHygieneCategoryClient
    {
        string ChangeToPostImportCategory(string downloadId, MediaKind kind);
    }
}
