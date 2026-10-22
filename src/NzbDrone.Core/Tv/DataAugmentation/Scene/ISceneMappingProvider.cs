using System.Collections.Generic;

namespace NzbDrone.Core.Tv.DataAugmentation.Scene
{
    public interface ISceneMappingProvider
    {
        List<SceneMapping> GetSceneMappings();
    }
}
