using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Moq;
using NLog;
using NUnit.Framework;
using Radarr.Http.ErrorManagement;

namespace NzbDrone.Api.Test.Http.ErrorManagement
{
    [TestFixture]
    public class RadarrErrorPipelineFixture
    {
        [Test]
        public async Task unexpected_error_response_omits_exception_type_and_stack()
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();
            var feature = new Mock<IExceptionHandlerPathFeature>();
            feature.SetupGet(item => item.Error).Returns(new InvalidOperationException("synthetic failure"));
            context.Features.Set(feature.Object);

            await new RadarrErrorPipeline(LogManager.GetLogger(nameof(RadarrErrorPipelineFixture))).HandleException(context);

            context.Response.StatusCode.Should().Be(500);
            context.Response.Body.Position = 0;
            var body = await new StreamReader(context.Response.Body, Encoding.UTF8).ReadToEndAsync();
            body.Should().Contain("synthetic failure");
            body.Should().NotContain("InvalidOperationException");
            body.Should().NotContain(" at ");
        }
    }
}
