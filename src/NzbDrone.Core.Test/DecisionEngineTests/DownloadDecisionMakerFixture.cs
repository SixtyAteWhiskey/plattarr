using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Search;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class DownloadDecisionMakerFixture : CoreTest<DownloadDecisionMaker>
    {
        private List<ReleaseInfo> _reports;
        private RemoteMovie _remoteMovie;

        private Mock<IDownloadDecisionEngineSpecification> _pass1;
        private Mock<IDownloadDecisionEngineSpecification> _pass2;
        private Mock<IDownloadDecisionEngineSpecification> _pass3;

        private Mock<IDownloadDecisionEngineSpecification> _fail1;
        private Mock<IDownloadDecisionEngineSpecification> _fail2;
        private Mock<IDownloadDecisionEngineSpecification> _fail3;

        [SetUp]
        public void Setup()
        {
            _pass1 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass2 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass3 = new Mock<IDownloadDecisionEngineSpecification>();

            _fail1 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail2 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail3 = new Mock<IDownloadDecisionEngineSpecification>();

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail3"));

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "Trolls.2016.720p.WEB-DL.DD5.1.H264-FGT" } };
            _remoteMovie = new RemoteMovie
            {
                Movie = new Movie(),
                ParsedMovieInfo = new ParsedMovieInfo()
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>())).Returns(_remoteMovie);

            // The decision maker parses through the movie matcher; use the real one so these
            // tests keep covering Radarr's parse and map path with the mocked parsing service.
            Mocker.SetConstant<IMovieReleaseMatcher>(Mocker.Resolve<MovieReleaseMatcher>());

            GivenReleaseSpecifications();
        }

        private void GivenSpecifications(params Mock<IDownloadDecisionEngineSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(mocks.Select(c => c.Object));
        }

        private void GivenReleaseSpecifications(params Mock<IReleaseSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IReleaseSpecification>>(mocks.Select(c => c.Object));
        }

        private static Mock<IDownloadDecisionEngineSpecification> MovieSpec(SpecificationPriority priority, bool accept)
        {
            var mock = new Mock<IDownloadDecisionEngineSpecification>();
            mock.SetupGet(c => c.Priority).Returns(priority);
            mock.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()))
                .Returns(accept ? DownloadSpecDecision.Accept() : DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "movie spec"));

            return mock;
        }

        private static Mock<IReleaseSpecification> CoreSpec(SpecificationPriority priority, bool accept)
        {
            var mock = new Mock<IReleaseSpecification>();
            mock.SetupGet(c => c.Priority).Returns(priority);
            mock.SetupGet(c => c.Type).Returns(RejectionType.Temporary);
            mock.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteRelease>(), It.IsAny<ReleaseDecisionInformation>()))
                .Returns(accept ? DownloadSpecDecision.Accept() : DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "core spec"));

            return mock;
        }

        [Test]
        public void should_call_core_release_specifications_with_the_remote_movie()
        {
            var core = CoreSpec(SpecificationPriority.Default, true);
            GivenSpecifications(_pass1);
            GivenReleaseSpecifications(core);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
            core.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_reject_with_core_release_specification_type_and_message()
        {
            GivenSpecifications(_pass1);
            GivenReleaseSpecifications(CoreSpec(SpecificationPriority.Default, false));

            var result = Subject.GetRssDecision(_reports).Single();

            result.Approved.Should().BeFalse();
            result.Rejections.Should().ContainSingle();
            result.Rejections.Single().Message.Should().Be("core spec");
            result.Rejections.Single().Type.Should().Be(RejectionType.Temporary);
        }

        [Test]
        public void should_not_run_core_disk_specification_when_movie_specification_rejects_first()
        {
            var coreDisk = CoreSpec(SpecificationPriority.Disk, true);
            GivenSpecifications(MovieSpec(SpecificationPriority.Default, false));
            GivenReleaseSpecifications(coreDisk);

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().Equal("movie spec");
            coreDisk.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteRelease>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_run_movie_disk_specification_when_core_specification_rejects_first()
        {
            var movieDisk = MovieSpec(SpecificationPriority.Disk, true);
            GivenSpecifications(movieDisk);
            GivenReleaseSpecifications(CoreSpec(SpecificationPriority.Default, false));

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().Equal("core spec");
            movieDisk.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_collect_rejections_from_both_sets_in_the_same_priority_group()
        {
            GivenSpecifications(MovieSpec(SpecificationPriority.Default, false));
            GivenReleaseSpecifications(CoreSpec(SpecificationPriority.Default, false));

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().BeEquivalentTo("core spec", "movie spec");
        }

        [Test]
        public void should_call_all_specifications()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetRssDecision(_reports).ToList();

            _fail1.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(_remoteMovie, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_return_rejected_if_single_specs_fail()
        {
            GivenSpecifications(_fail1);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_rejected_if_one_of_specs_fail()
        {
            GivenSpecifications(_pass1, _fail1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeFalse();
        }

        [Test]
        public void should_return_pass_if_all_specs_pass()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
        }

        [Test]
        public void should_have_same_number_of_rejections_as_specs_that_failed()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            var result = Subject.GetRssDecision(_reports);
            result.Single().Rejections.Should().HaveCount(3);
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_not_parsable()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "Not parsable";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_return_rejected_result_for_unparsable_search()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            Subject.GetSearchDecision(_reports, new MovieSearchRequest()).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_make_decision_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteMovie.Movie = null;

            Subject.GetRssDecision(_reports);

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteMovie>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void broken_report_shouldnt_blowup_the_process()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Trolls.2016.720p.WEB-DL.DD5.1.H264-FGT" },
                    new ReleaseInfo { Title = "Trolls.2016.720p.WEB-DL.DD5.1.H264-FGT" },
                    new ReleaseInfo { Title = "Trolls.2016.720p.WEB-DL.DD5.1.H264-FGT" }
                };

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>()), Times.Exactly(_reports.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteMovie.Movie = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_not_allow_download_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteMovie.Movie = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            // result.First().RemoteMovie.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        [Ignore("Series")]
        public void should_not_allow_download_if_no_episodes_found()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteMovie.Movie = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            // result.First().RemoteMovie.DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedMovieInfo>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MovieSearchRequest>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "Trolls.2016.720p.WEB-DL.DD5.1.H264-FGT" },
                };

            Subject.GetRssDecision(_reports).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}
