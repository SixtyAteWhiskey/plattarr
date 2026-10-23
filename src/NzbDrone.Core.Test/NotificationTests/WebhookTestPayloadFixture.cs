using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Movies.Notifications;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Notifications.Webhook;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.Notifications;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class WebhookTestPayloadFixture : CoreTest<Webhook>
    {
        private List<string> _sent;

        [SetUp]
        public void Setup()
        {
            // Registered out of kind order on purpose: the test sends them in kind order.
            Mocker.SetConstant<IEnumerable<IWebhookPayloadBuilder>>(new IWebhookPayloadBuilder[]
            {
                Mocker.Resolve<TvWebhookPayloadBuilder>(),
                Mocker.Resolve<MovieWebhookPayloadBuilder>()
            });

            Subject.Definition = new NotificationDefinition { Settings = new WebhookSettings { Url = "http://x", Method = 1 } };

            _sent = new List<string>();
            Mocker.GetMock<IWebhookProxy>()
                  .Setup(p => p.SendWebhook(It.IsAny<WebhookPayload>(), It.IsAny<WebhookSettings>()))
                  .Callback<WebhookPayload, WebhookSettings>((p, s) => _sent.Add(p.ToJson(Newtonsoft.Json.Formatting.None)));
        }

        [Test]
        public void should_send_a_test_payload_in_every_modules_shape()
        {
            Subject.Test().IsValid.Should().BeTrue();

            _sent.Should().HaveCount(2);
            _sent.Should().OnlyContain(json => json.Contains("\"eventType\":\"Test\""));
            _sent[0].Should().Contain("\"movie\":").And.NotContain("\"series\":");
            _sent[1].Should().Contain("\"series\":").And.NotContain("\"movie\":");
        }

        [Test]
        public void should_fail_the_test_when_the_receiver_refuses_a_payload()
        {
            Mocker.GetMock<IWebhookProxy>()
                  .Setup(p => p.SendWebhook(It.Is<WebhookPayload>(w => w.ToJson(Newtonsoft.Json.Formatting.None).Contains("\"series\":")), It.IsAny<WebhookSettings>()))
                  .Throws(new WebhookException("refused"));

            var result = Subject.Test();

            result.IsValid.Should().BeFalse();
            result.Errors.Single().PropertyName.Should().Be("Url");
            _sent.Should().HaveCount(1);
        }
    }
}
