using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Messaging;
using NzbDrone.Common.TPL;
using NzbDrone.Core.HealthCheck;
using NzbDrone.Core.HealthCheck.Checks;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.HealthCheck
{
    public class HealthCheckServiceFixture : CoreTest<HealthCheckService>
    {
        private FakeHealthCheck _healthCheck;

        [SetUp]
        public void SetUp()
        {
            _healthCheck = new FakeHealthCheck();

            Mocker.SetConstant<IEnumerable<IProvideHealthCheck>>(new[] { _healthCheck });
            Mocker.SetConstant<ICacheManager>(Mocker.Resolve<CacheManager>());
            Mocker.SetConstant<IDebounceManager>(Mocker.Resolve<DebounceManager>());

            Mocker.GetMock<IDebounceManager>().Setup(s => s.CreateDebouncer(It.IsAny<Action>(), It.IsAny<TimeSpan>()))
                .Returns<Action, TimeSpan>((a, t) => new MockDebouncer(a, t));
        }

        [Test]
        public void should_not_execute_conditional()
        {
            Subject.HandleAsync(new FakeEvent());

            _healthCheck.Executed.Should().BeFalse();
        }

        [Test]
        public void should_execute_conditional()
        {
            Subject.HandleAsync(new FakeEvent() { ShouldExecute = true });

            _healthCheck.Executed.Should().BeTrue();
        }

        [Test]
        public void should_execute_unconditional()
        {
            Subject.HandleAsync(new FakeEvent2());

            _healthCheck.Executed.Should().BeTrue();
        }

        [TestCase(1, nameof(IndexerStatusCheck))]
        [TestCase(7, nameof(IndexerLongTermStatusCheck))]
        public void should_clear_indexer_health_when_backoff_expires(int failureAgeHours, string checkName)
        {
            var indexer = new Mock<IIndexer>();
            indexer.SetupGet(i => i.Definition).Returns(new IndexerDefinition { Id = 1 });
            var status = new IndexerStatus
            {
                ProviderId = 1,
                InitialFailure = DateTime.UtcNow.AddHours(-failureAgeHours),
                DisabledTill = DateTime.UtcNow.AddMinutes(1)
            };

            Mocker.GetMock<IIndexerFactory>()
                  .Setup(f => f.GetAvailableProviders())
                  .Returns(new List<IIndexer> { indexer.Object });
            Mocker.GetMock<IIndexerStatusService>()
                  .Setup(s => s.GetBlockedProviders())
                  .Returns(() => status.IsDisabled() ? new List<IndexerStatus> { status } : new List<IndexerStatus>());
            Mocker.GetMock<ILocalizationService>()
                  .Setup(s => s.GetLocalizedString(It.IsAny<string>()))
                  .Returns("blocked");
            Mocker.SetConstant<IEnumerable<IProvideHealthCheck>>(new IProvideHealthCheck[]
            {
                _healthCheck,
                new IndexerStatusCheck(Mocker.GetMock<IIndexerFactory>().Object, Mocker.GetMock<IIndexerStatusService>().Object, Mocker.GetMock<ILocalizationService>().Object),
                new IndexerLongTermStatusCheck(Mocker.GetMock<IIndexerFactory>().Object, Mocker.GetMock<IIndexerStatusService>().Object, Mocker.GetMock<ILocalizationService>().Object)
            });

            Subject.Execute(new CheckIndexerStatusHealthCommand());
            Subject.Results().Select(r => r.Source.Name).Should().Contain(checkName);

            status.DisabledTill = DateTime.UtcNow.AddMinutes(-1);
            Subject.Execute(new CheckIndexerStatusHealthCommand());

            Subject.Results().Should().BeEmpty();
            _healthCheck.Executed.Should().BeFalse();
        }
    }

    public class FakeEvent : IEvent
    {
        public bool ShouldExecute { get; set; }
    }

    public class FakeEvent2 : IEvent
    {
        public bool ShouldExecute { get; set; }
    }

    [CheckOn(typeof(FakeEvent))]
    [CheckOn(typeof(FakeEvent2))]
    public class FakeHealthCheck : IProvideHealthCheck, ICheckOnCondition<FakeEvent>
    {
        public bool CheckOnStartup => false;
        public bool CheckOnSchedule => false;

        public bool Executed { get; set; }
        public bool Checked { get; set; }

        public Core.HealthCheck.HealthCheck Check()
        {
            Executed = true;

            return new Core.HealthCheck.HealthCheck(GetType());
        }

        public bool ShouldCheckOnEvent(FakeEvent message)
        {
            return message.ShouldExecute;
        }
    }
}
