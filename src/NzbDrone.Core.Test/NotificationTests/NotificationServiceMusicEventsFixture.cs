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
    // The three events step 7 added (album delete, download failure, retag) go through the same rules as
    // every other event: the providers enabled for it, the notification tags against the subject's tags,
    // and a recorded success or failure per provider.
    [TestFixture]
    public class NotificationServiceMusicEventsFixture : CoreTest<NotificationService>
    {
        public class Scenario
        {
            public string Name { get; set; }
            public NotificationEvent Event { get; set; }
            public Action<Mock<INotificationFactory>, List<INotification>> SetupFactory { get; set; }
            public Action<NotificationService, ActivitySubject> Send { get; set; }
            public Action<Mock<INotification>, Times> Verify { get; set; }
            public Action<Mock<INotification>> Throw { get; set; }

            public override string ToString() => Name;
        }

        public static IEnumerable<Scenario> Scenarios()
        {
            var delete = new ItemDeleteMessage();
            var failure = new DownloadFailureMessage();
            var retag = new RetagMessage();

            yield return new Scenario
            {
                Name = "ItemDelete",
                Event = NotificationEvent.ItemDelete,
                SetupFactory = (f, l) => f.Setup(x => x.OnAlbumDeleteEnabled(true)).Returns(l),
                Send = (s, subject) =>
                {
                    delete.Subject = subject;
                    s.ItemDelete(delete);
                },
                Verify = (p, times) => p.Verify(x => x.OnItemDelete(delete), times),
                Throw = p => p.Setup(x => x.OnItemDelete(It.IsAny<ItemDeleteMessage>())).Throws(new InvalidOperationException("boom"))
            };

            yield return new Scenario
            {
                Name = "DownloadFailure",
                Event = NotificationEvent.DownloadFailure,
                SetupFactory = (f, l) => f.Setup(x => x.OnDownloadFailureEnabled(true)).Returns(l),
                Send = (s, subject) =>
                {
                    failure.Subject = subject;
                    s.DownloadFailure(failure);
                },
                Verify = (p, times) => p.Verify(x => x.OnDownloadFailure(failure), times),
                Throw = p => p.Setup(x => x.OnDownloadFailure(It.IsAny<DownloadFailureMessage>())).Throws(new InvalidOperationException("boom"))
            };

            yield return new Scenario
            {
                Name = "Retag",
                Event = NotificationEvent.Retag,
                SetupFactory = (f, l) => f.Setup(x => x.OnTrackRetagEnabled(true)).Returns(l),
                Send = (s, subject) =>
                {
                    retag.Subject = subject;
                    s.Retag(retag);
                },
                Verify = (p, times) => p.Verify(x => x.OnRetag(retag), times),
                Throw = p => p.Setup(x => x.OnRetag(It.IsAny<RetagMessage>())).Throws(new InvalidOperationException("boom"))
            };
        }

        private Mock<INotification> NewProvider(int id, HashSet<int> tags = null)
        {
            var provider = new Mock<INotification>();
            provider.SetupGet(p => p.Definition).Returns(new NotificationDefinition
            {
                Id = id,
                Name = "Provider " + id,
                Tags = tags ?? new HashSet<int>()
            });

            return provider;
        }

        private static ActivitySubject TaggedSubject(params int[] tags) => new ActivitySubject { Tags = new HashSet<int>(tags) };

        [TestCaseSource(nameof(Scenarios))]
        public void should_send_to_a_provider_enabled_for_the_event_and_record_the_success(Scenario scenario)
        {
            var provider = NewProvider(7);
            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification> { provider.Object });

            scenario.Send(Subject, TaggedSubject(1));

            scenario.Verify(provider, Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(7), Times.Once());
        }

        [TestCaseSource(nameof(Scenarios))]
        public void should_send_when_the_notification_tags_intersect_the_subject_tags(Scenario scenario)
        {
            var provider = NewProvider(7, new HashSet<int> { 2, 9 });
            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification> { provider.Object });

            scenario.Send(Subject, TaggedSubject(1, 2));

            scenario.Verify(provider, Times.Once());
        }

        [TestCaseSource(nameof(Scenarios))]
        public void should_not_send_when_the_notification_tags_do_not_match(Scenario scenario)
        {
            var provider = NewProvider(7, new HashSet<int> { 9 });
            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification> { provider.Object });

            scenario.Send(Subject, TaggedSubject(1, 2));

            scenario.Verify(provider, Times.Never());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(It.IsAny<int>()), Times.Never());
        }

        [TestCaseSource(nameof(Scenarios))]
        public void should_record_a_failure_and_carry_on_with_the_next_provider(Scenario scenario)
        {
            var first = NewProvider(7);
            var second = NewProvider(8);
            scenario.Throw(first);
            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification> { first.Object, second.Object });

            scenario.Send(Subject, TaggedSubject(1));

            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordFailure(7), Times.Once());
            scenario.Verify(second, Times.Once());
            Mocker.GetMock<INotificationStatusService>().Verify(s => s.RecordSuccess(8), Times.Once());
        }

        [TestCaseSource(nameof(Scenarios))]
        public void should_report_the_event_enabled_only_when_a_provider_wants_it(Scenario scenario)
        {
            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification>());
            Subject.AnyEnabled(scenario.Event).Should().BeFalse();

            scenario.SetupFactory(Mocker.GetMock<INotificationFactory>(), new List<INotification> { NewProvider(7).Object });
            Subject.AnyEnabled(scenario.Event).Should().BeTrue();
        }
    }
}
