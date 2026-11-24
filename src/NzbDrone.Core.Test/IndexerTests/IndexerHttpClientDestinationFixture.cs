using System;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Indexers;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.IndexerTests
{
    [TestFixture]
    public class IndexerHttpClientDestinationFixture : TestBase<IndexerHttpClient>
    {
        [TestCase("http://127.0.0.1:9697/download")]
        [TestCase("http://169.254.169.254/private")]
        public async Task should_reject_other_private_origins_before_a_proxy_can_rewrite_them(string destination)
        {
            var request = new HttpRequest(destination)
            {
                PublicDestinationOnly = true,
                ConfiguredOrigin = new Uri("http://127.0.0.1:9696/api")
            };
            var definition = new IndexerDefinition { Id = 1 };

            await FluentActions.Invoking(() => Subject.ExecuteProxiedAsync(request, definition))
                               .Should().ThrowAsync<WebException>();
            FluentActions.Invoking(() => Subject.ExecuteProxied(request, definition))
                         .Should().Throw<WebException>();
        }
    }
}
