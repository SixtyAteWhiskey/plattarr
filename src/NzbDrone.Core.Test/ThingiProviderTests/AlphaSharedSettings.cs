using NzbDrone.Core.ThingiProvider;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Test.ThingiProviderTests.Alpha
{
    // Test-local settings type that shares its name with the one in the other test namespace.
    public class AlphaDefinition : ProviderDefinition
    {
    }

    public class SharedSettings : IProviderConfig
    {
        public NzbDroneValidationResult Validate() => new();
    }
}
