using NUnit.Framework;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.HealthCheck.Checks
{
    [TestFixture]
    public class SystemTimeCheckFixture : CoreTest<SystemTimeCheck>
    {
        [Test]
        public void should_not_contact_an_upstream_time_service()
        {
            Subject.Check().ShouldBeOk();
            Mocker.GetMock<NzbDrone.Common.Http.IHttpClient>().VerifyNoOtherCalls();
        }
    }
}
