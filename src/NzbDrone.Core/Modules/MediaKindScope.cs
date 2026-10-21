using System.Collections.Generic;

namespace NzbDrone.Core.Modules
{
    // A global setting (quality profile, custom format) that may be limited to some
    // media types. An empty AppliesTo means every media type the setting can serve.
    public interface IMediaKindScoped
    {
        List<MediaKind> AppliesTo { get; set; }
    }

    public static class MediaKindScopeExtensions
    {
        public static bool AppliesToKind(this IMediaKindScoped setting, MediaKind kind)
        {
            return setting.AppliesTo == null || setting.AppliesTo.Count == 0 || setting.AppliesTo.Contains(kind);
        }
    }
}
