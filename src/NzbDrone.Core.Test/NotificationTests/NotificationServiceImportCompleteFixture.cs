using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests
{
    [TestFixture]
    public class NotificationServiceImportCompleteFixture : CoreTest<NotificationService>
    {
        private Mock<INotification> _provider;
        private ImportCompleteMessage _message;

        [SetUp]
        public void Setup()
        {
            _provider = new Mock<INotification>();
            _provider.SetupGet(p => p.Definition).Returns(new NotificationDefinition { Id = 7, Name = "Hook", OnImportComplete = true });

            Mocker.GetMock<INotificationFactory>()
                  .Setup(f => f.OnImportCompleteEnabled(true))
                  .Returns(new List<INotification> { _provider.Object });

            _message = new ImportCompleteMessage
            {
                Message = "Game of Thrones - Season 1 [WEBDL-1080p]",
                Subject = new ActivitySubject { Tags = new HashSet<int> { 1, 2 } }
            };
        }

        [Test]
        public void should_send_to_a_provider_enabled_for_the_event_and_record_the_success()
        {
            Subject.ImportComplete(_message);

            _provider.Verify(p => p.OnImportComplete(_message), Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(7), Times.Once());
        }

        [Test]
        public void should_send_when_the_notification_tags_intersect_the_series_tags()
        {
            _provider.Object.Definition.Tags = new HashSet<int> { 2, 9 };

            Subject.ImportComplete(_message);

            _provider.Verify(p => p.OnImportComplete(_message), Times.Once());
        }

        [Test]
        public void should_not_send_when_the_notification_tags_do_not_match()
        {
            _provider.Object.Definition.Tags = new HashSet<int> { 9 };

            Subject.ImportComplete(_message);

            _provider.Verify(p => p.OnImportComplete(It.IsAny<ImportCompleteMessage>()), Times.Never());
        }

        [Test]
        public void should_record_a_failure_and_carry_on_with_the_next_provider()
        {
            var second = new Mock<INotification>();
            second.SetupGet(p => p.Definition).Returns(new NotificationDefinition { Id = 8, Name = "Second", OnImportComplete = true });
            _provider.Setup(p => p.OnImportComplete(It.IsAny<ImportCompleteMessage>())).Throws(new InvalidOperationException("boom"));

            Mocker.GetMock<INotificationFactory>()
                  .Setup(f => f.OnImportCompleteEnabled(true))
                  .Returns(new List<INotification> { _provider.Object, second.Object });

            Subject.ImportComplete(_message);

            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordFailure(7), Times.Once());
            second.Verify(p => p.OnImportComplete(_message), Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(8), Times.Once());
        }

        [Test]
        public void should_report_the_event_enabled_when_a_provider_wants_it()
        {
            Subject.AnyEnabled(NotificationEvent.ImportComplete).Should().BeTrue();

            Mocker.GetMock<INotificationFactory>()
                  .Setup(f => f.OnImportCompleteEnabled(true))
                  .Returns(new List<INotification>());

            Subject.AnyEnabled(NotificationEvent.ImportComplete).Should().BeFalse();
        }

        [Test]
        public void should_enable_a_definition_that_has_only_this_event_on()
        {
            new NotificationDefinition { OnImportComplete = true }.Enable.Should().BeTrue();
            new NotificationDefinition().Enable.Should().BeFalse();
        }
    }
}
