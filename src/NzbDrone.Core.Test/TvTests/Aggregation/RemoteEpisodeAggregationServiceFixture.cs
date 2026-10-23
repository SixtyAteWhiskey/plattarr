using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.Aggregation.Aggregators;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.Aggregation
{
    [TestFixture]
    public class RemoteEpisodeAggregationServiceFixture : CoreTest<RemoteEpisodeAggregationService>
    {
        private Mock<IAggregateRemoteEpisode> _first;
        private Mock<IAggregateRemoteEpisode> _second;

        [SetUp]
        public void Setup()
        {
            _first = new Mock<IAggregateRemoteEpisode>();
            _second = new Mock<IAggregateRemoteEpisode>();

            Mocker.SetConstant<IEnumerable<IAggregateRemoteEpisode>>(new[] { _first.Object, _second.Object });
        }

        [Test]
        public void should_return_null_for_a_null_remote_episode()
        {
            Subject.Augment(null).Should().BeNull();

            _first.Verify(a => a.Aggregate(It.IsAny<RemoteEpisode>()), Times.Never());
        }

        [Test]
        public void should_run_every_aggregator_on_the_remote_episode()
        {
            var remoteEpisode = new RemoteEpisode();

            Subject.Augment(remoteEpisode).Should().BeSameAs(remoteEpisode);

            _first.Verify(a => a.Aggregate(remoteEpisode), Times.Once());
            _second.Verify(a => a.Aggregate(remoteEpisode), Times.Once());
        }

        [Test]
        public void should_keep_going_when_an_aggregator_throws()
        {
            var remoteEpisode = new RemoteEpisode();

            _first.Setup(a => a.Aggregate(It.IsAny<RemoteEpisode>())).Throws(new InvalidOperationException("boom"));

            Subject.Augment(remoteEpisode).Should().BeSameAs(remoteEpisode);

            _second.Verify(a => a.Aggregate(remoteEpisode), Times.Once());
        }
    }
}
