using System.Collections.Generic;

namespace NzbDrone.Core.Modules
{
    // What the core needs to know about a parent entity (Movie, Series, Artist).
    public interface IMediaParent
    {
        MediaKind Kind { get; }
        int Id { get; }
        string Title { get; }
        string Path { get; }
        HashSet<int> Tags { get; }
        int QualityProfileId { get; }
        bool Monitored { get; }
    }
}
