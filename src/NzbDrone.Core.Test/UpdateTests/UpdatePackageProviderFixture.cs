using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [Test]
        public void does_not_fetch_radarr_update_packages()
        {
            Subject.GetLatestUpdate("develop", new System.Version(10, 0)).Should().BeNull();
        }

        [Test]
        public void reports_no_recent_packages_until_plattarr_has_its_own_feed()
        {
            Subject.GetRecentUpdates("develop", new System.Version(10, 0)).Should().BeEmpty();
        }
    }
}
