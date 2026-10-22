using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    // The core prioritizer only splits decisions by media type; each module orders its own.
    [TestFixture]
    public class PrioritizeDownloadDecisionRoutingFixture : CoreTest<PrioritizeDownloadDecision>
    {
        private Mock<IDownloadDecisionPrioritizer> _movies;
        private Mock<IDownloadDecisionPrioritizer> _tv;

        [SetUp]
        public void Setup()
        {
            _movies = GivenPrioritizer(MediaKind.Movies);
            _tv = GivenPrioritizer(MediaKind.Tv);

            Mocker.SetConstant<IEnumerable<IDownloadDecisionPrioritizer>>(new[] { _movies.Object, _tv.Object });
        }

        private static Mock<IDownloadDecisionPrioritizer> GivenPrioritizer(MediaKind kind)
        {
            // Reverses its input so a test can tell the module's order from the input order.
            var mock = new Mock<IDownloadDecisionPrioritizer>();
            mock.SetupGet(p => p.Kind).Returns(kind);
            mock.Setup(p => p.Prioritize(It.IsAny<List<DownloadDecision>>()))
                .Returns<List<DownloadDecision>>(d => Enumerable.Reverse(d).ToList());

            return mock;
        }

        private static DownloadDecision Movie()
        {
            return new DownloadDecision(new RemoteMovie { Release = new ReleaseInfo { Title = "movie" } });
        }

        private static DownloadDecision Tv()
        {
            return new DownloadDecision(new TvRemoteRelease { Release = new ReleaseInfo { Title = "tv" } });
        }

        [Test]
        public void should_hand_each_prioritizer_only_its_own_media_type()
        {
            var movie1 = Movie();
            var movie2 = Movie();
            var tv1 = Tv();

            Subject.PrioritizeDecisions(new List<DownloadDecision> { movie1, tv1, movie2 });

            _movies.Verify(p => p.Prioritize(It.Is<List<DownloadDecision>>(l => l.SequenceEqual(new[] { movie1, movie2 }))), Times.Once());
            _tv.Verify(p => p.Prioritize(It.Is<List<DownloadDecision>>(l => l.SequenceEqual(new[] { tv1 }))), Times.Once());
        }

        [Test]
        public void should_return_the_order_each_module_chose()
        {
            var movie1 = Movie();
            var movie2 = Movie();

            var result = Subject.PrioritizeDecisions(new List<DownloadDecision> { movie1, movie2 });

            result.Should().Equal(movie2, movie1);
        }

        [Test]
        public void should_keep_a_media_type_without_a_prioritizer_at_the_end()
        {
            Mocker.SetConstant<IEnumerable<IDownloadDecisionPrioritizer>>(new[] { _movies.Object });

            var tv = Tv();
            var movie = Movie();

            var result = Subject.PrioritizeDecisions(new List<DownloadDecision> { tv, movie });

            result.Should().Equal(movie, tv);
        }

        [Test]
        public void should_not_lose_or_duplicate_decisions()
        {
            var decisions = new List<DownloadDecision> { Movie(), Tv(), Movie(), Tv(), Movie() };

            var result = Subject.PrioritizeDecisions(decisions);

            result.Should().HaveCount(decisions.Count);
            result.Distinct().Should().HaveCount(decisions.Count);
            decisions.Should().OnlyContain(d => result.Contains(d));
        }

        [Test]
        public void should_return_an_empty_list_for_no_decisions()
        {
            Subject.PrioritizeDecisions(new List<DownloadDecision>()).Should().BeEmpty();
        }
    }
}
