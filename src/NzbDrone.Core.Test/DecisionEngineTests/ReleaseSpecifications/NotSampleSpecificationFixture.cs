using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications
{
    [TestFixture]
    public class NotSampleSpecificationFixture : CoreTest<NotSampleSpecification>
    {
        private static RemoteMovie Movie(string title, long? size)
        {
            return new RemoteMovie { Movie = new Movie(), Release = new ReleaseInfo { Title = title, Size = size } };
        }

        [Test]
        public void should_reject_small_sample()
        {
            var result = Subject.IsSatisfiedBy(Movie("Interstellar.2014.1080p.BluRay.x264-SPARKS.sample", 20.Megabytes()), new());

            result.Accepted.Should().BeFalse();
            result.Reason.Should().Be(DownloadRejectionReason.Sample);
        }

        [Test]
        public void should_reject_sample_with_unknown_size()
        {
            Subject.IsSatisfiedBy(Movie("Interstellar.2014.1080p.BluRay.x264-SPARKS.sample", null), new()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_large_release_with_sample_in_title()
        {
            Subject.IsSatisfiedBy(Movie("Interstellar.2014.1080p.BluRay.x264-SPARKS.sample", 8000.Megabytes()), new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_small_release_without_sample_in_title()
        {
            Subject.IsSatisfiedBy(Movie("Interstellar.2014.480p.WEB.x264-GRP", 20.Megabytes()), new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_small_sample_for_a_non_movie_release()
        {
            var release = new TvRemoteRelease { Release = new ReleaseInfo { Title = "Dexter.S08E01.720p.HDTV.x264-IMMERSE.sample", Size = 20.Megabytes() } };

            Subject.IsSatisfiedBy(release, new()).Accepted.Should().BeFalse();
        }
    }
}
