using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.ReleaseSpecifications;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Aggregation;
using NzbDrone.Core.Tv.CustomFormats;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.DecisionEngine;
using NzbDrone.Core.Tv.DecisionEngine.Specifications;
using NzbDrone.Core.Tv.Parser;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests.DecisionEngineTests
{
    [TestFixture]
    public class DownloadDecisionMakerFixture : CoreTest<DownloadDecisionMaker>
    {
        private List<ReleaseInfo> _reports;
        private RemoteEpisode _remoteEpisode;

        private Mock<IDownloadDecisionEngineSpecification> _pass1;
        private Mock<IDownloadDecisionEngineSpecification> _pass2;
        private Mock<IDownloadDecisionEngineSpecification> _pass3;

        private Mock<IDownloadDecisionEngineSpecification> _fail1;
        private Mock<IDownloadDecisionEngineSpecification> _fail2;
        private Mock<IDownloadDecisionEngineSpecification> _fail3;

        private Mock<IDownloadDecisionEngineSpecification> _failDelayed1;

        [SetUp]
        public void Setup()
        {
            _pass1 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass2 = new Mock<IDownloadDecisionEngineSpecification>();
            _pass3 = new Mock<IDownloadDecisionEngineSpecification>();

            _fail1 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail2 = new Mock<IDownloadDecisionEngineSpecification>();
            _fail3 = new Mock<IDownloadDecisionEngineSpecification>();

            _failDelayed1 = new Mock<IDownloadDecisionEngineSpecification>();

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);
            _pass3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Accept);

            _fail1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail1"));
            _fail2.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail2"));
            _fail3.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "fail3"));

            _failDelayed1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Returns(DownloadSpecDecision.Reject(DownloadRejectionReason.MinimumAgeDelay, "failDelayed1"));
            _failDelayed1.SetupGet(c => c.Priority).Returns(SpecificationPriority.Disk);

            _reports = new List<ReleaseInfo> { new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" } };
            _remoteEpisode = new RemoteEpisode
            {
                ParsedEpisodeInfo = NzbDrone.Core.Tv.Parser.Parser.ParseTitle(_reports.First().Title),
                Series = new Series(),
                Episodes = new List<Episode> { new Episode() }
            };

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                  .Returns(_remoteEpisode);

            // The decision maker parses through the TV matcher; use the real one so these tests
            // keep covering Sonarr's parse and map path with the mocked parsing service.
            Mocker.SetConstant<ITvReleaseMatcher>(Mocker.Resolve<TvReleaseMatcher>());

            GivenReleaseSpecifications();
        }

        private void GivenReleaseSpecifications(params Mock<IReleaseSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IReleaseSpecification>>(mocks.Select(c => c.Object));
        }

        private static Mock<IDownloadDecisionEngineSpecification> TvSpec(SpecificationPriority priority, bool accept)
        {
            var mock = new Mock<IDownloadDecisionEngineSpecification>();
            mock.SetupGet(c => c.Priority).Returns(priority);
            mock.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()))
                .Returns(accept ? DownloadSpecDecision.Accept() : DownloadSpecDecision.Reject(DownloadRejectionReason.Unknown, "tv spec"));

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

        private void GivenSpecifications(params Mock<IDownloadDecisionEngineSpecification>[] mocks)
        {
            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(mocks.Select(c => c.Object));
        }

        [Test]
        public void should_call_all_specifications()
        {
            GivenSpecifications(_pass1, _pass2, _pass3, _fail1, _fail2, _fail3);

            Subject.GetRssDecision(_reports).ToList();

            _fail1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _fail3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass2.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass3.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_call_delayed_specifications_if_non_delayed_passed()
        {
            GivenSpecifications(_pass1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();
            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
        }

        [Test]
        public void should_not_call_delayed_specifications_if_non_delayed_failed()
        {
            GivenSpecifications(_fail1, _failDelayed1);

            Subject.GetRssDecision(_reports).ToList();

            _failDelayed1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Never());
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

            // Stripping the metadata leaves brackets rather than a title, so neither parser
            // recovers anything to map.
            _reports[0].Title = "[Not][parsable][1080p][x265(10bit)]";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_map_a_title_the_regexes_cannot_parse()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "Not parsable";

            Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()), Times.Once());
        }

        [Test]
        public void should_not_attempt_to_map_episode_if_series_title_is_blank()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            var results = Subject.GetRssDecision(_reports).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());

            results.Should().BeEmpty();
        }

        [Test]
        public void should_return_rejected_result_for_unparsable_search()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            Subject.GetSearchDecision(_reports, new SingleEpisodeSearchRequest()).ToList();

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()), Times.Never());

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_attempt_to_make_decision_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            Subject.GetRssDecision(_reports);

            _pass1.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass2.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
            _pass3.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void broken_report_shouldnt_blowup_the_process()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" }
                };

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IParsingService>().Verify(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()), Times.Exactly(_reports.Count));

            ExceptionVerification.ExpectedErrors(3);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_only_include_reports_for_requested_episodes()
        {
            var series = Builder<Series>.CreateNew().Build();

            var episodes = Builder<Episode>.CreateListOfSize(2)
                .All()
                .With(v => v.SeriesId, series.Id)
                .With(v => v.Series, series)
                .With(v => v.SeasonNumber, 1)
                .With(v => v.SceneSeasonNumber, 2)
                .BuildList();

            var criteria = new SeasonSearchRequest { Episodes = episodes.Take(1).ToList(), SeasonNumber = 1 };

            var reports = episodes.Select(v =>
                new ReleaseInfo()
                {
                    Title = string.Format("{0}.S{1:00}E{2:00}.720p.WEB-DL-DRONE", series.Title, v.SceneSeasonNumber, v.SceneEpisodeNumber)
                }).ToList();

            Mocker.GetMock<IParsingService>()
                .Setup(v => v.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                .Returns<ParsedEpisodeInfo, int, int, string, TvSearchRequest>((p, _, _, _, _) =>
                    new RemoteEpisode
                    {
                        DownloadAllowed = true,
                        ParsedEpisodeInfo = p,
                        Series = series,
                        Episodes = episodes.Where(v => v.SceneEpisodeNumber == p.EpisodeNumbers.First()).ToList()
                    });

            Mocker.SetConstant<IEnumerable<IDownloadDecisionEngineSpecification>>(new List<IDownloadDecisionEngineSpecification>
            {
                Mocker.Resolve<NzbDrone.Core.Tv.DecisionEngine.Specifications.Search.EpisodeRequestedSpecification>()
            });

            var decisions = Subject.GetSearchDecision(reports, criteria);

            var approvedDecisions = decisions.Where(v => v.Approved).ToList();

            approvedDecisions.Count.Should().Be(1);
        }

        [Test]
        public void should_not_allow_download_if_series_is_unknown()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().GetRemoteEpisode().DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_not_allow_download_if_no_episodes_found()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            _remoteEpisode.Episodes = new List<Episode>();

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);

            result.First().GetRemoteEpisode().DownloadAllowed.Should().BeFalse();
        }

        [Test]
        public void should_return_a_decision_when_exception_is_caught()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>().Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                     .Throws<TestException>();

            _reports = new List<ReleaseInfo>
                {
                    new ReleaseInfo { Title = "The.Office.S03E115.DVDRip.XviD-OSiTV" },
                };

            Subject.GetRssDecision(_reports).Should().HaveCount(1);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_return_unknown_series_rejection_if_series_title_is_an_alias_for_another_series()
        {
            GivenSpecifications(_pass1, _pass2, _pass3);

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindTvdbId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(12345);

            _remoteEpisode.Series = null;

            var result = Subject.GetRssDecision(_reports);

            result.Should().HaveCount(1);
            result.First().Rejections.First().Message.Should().Contain("12345");
        }

        [Test]
        public void should_call_core_release_specifications_with_the_remote_episode()
        {
            var core = CoreSpec(SpecificationPriority.Default, true);
            GivenSpecifications(_pass1);
            GivenReleaseSpecifications(core);

            var result = Subject.GetRssDecision(_reports);

            result.Single().Approved.Should().BeTrue();
            core.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
            _pass1.Verify(c => c.IsSatisfiedBy(_remoteEpisode, It.IsAny<ReleaseDecisionInformation>()), Times.Once());
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
        public void should_not_run_core_disk_specification_when_tv_specification_rejects_first()
        {
            var coreDisk = CoreSpec(SpecificationPriority.Disk, true);
            GivenSpecifications(TvSpec(SpecificationPriority.Default, false));
            GivenReleaseSpecifications(coreDisk);

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().Equal("tv spec");
            coreDisk.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteRelease>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_not_run_tv_disk_specification_when_core_specification_rejects_first()
        {
            var tvDisk = TvSpec(SpecificationPriority.Disk, true);
            GivenSpecifications(tvDisk);
            GivenReleaseSpecifications(CoreSpec(SpecificationPriority.Default, false));

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().Equal("core spec");
            tvDisk.Verify(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()), Times.Never());
        }

        [Test]
        public void should_collect_rejections_from_both_sets_in_the_same_priority_group()
        {
            GivenSpecifications(TvSpec(SpecificationPriority.Default, false));
            GivenReleaseSpecifications(CoreSpec(SpecificationPriority.Default, false));

            var result = Subject.GetRssDecision(_reports).Single();

            result.Rejections.Select(r => r.Message).Should().BeEquivalentTo("core spec", "tv spec");
        }

        [Test]
        public void should_return_unknown_series_reason_when_series_is_unknown_and_not_an_alias()
        {
            GivenSpecifications(_pass1);
            _remoteEpisode.Series = null;

            var rejection = Subject.GetRssDecision(_reports).Single().Rejections.Single();

            rejection.Reason.Should().Be(DownloadRejectionReason.UnknownSeries);
        }

        [Test]
        public void should_return_matches_another_series_reason_for_an_alias()
        {
            GivenSpecifications(_pass1);
            _remoteEpisode.Series = null;

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindTvdbId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns(12345);

            var rejection = Subject.GetRssDecision(_reports).Single().Rejections.Single();

            rejection.Reason.Should().Be(DownloadRejectionReason.MatchesAnotherSeries);
        }

        [Test]
        public void should_return_unknown_episode_reason_when_no_episodes_found()
        {
            GivenSpecifications(_pass1);
            _remoteEpisode.Episodes = new List<Episode>();

            var rejection = Subject.GetRssDecision(_reports).Single().Rejections.Single();

            rejection.Reason.Should().Be(DownloadRejectionReason.UnknownEpisode);
        }

        [Test]
        public void should_return_unable_to_parse_reason_for_an_unparsable_search_release()
        {
            GivenSpecifications(_pass1);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            var decision = Subject.GetSearchDecision(_reports, new SingleEpisodeSearchRequest()).Single();

            decision.Rejections.Single().Reason.Should().Be(DownloadRejectionReason.UnableToParse);
            decision.GetRemoteEpisode().Release.Should().BeSameAs(_reports[0]);
        }

        [Test]
        public void should_not_return_a_decision_for_an_unparsable_rss_release()
        {
            GivenSpecifications(_pass1);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves";

            Subject.GetRssDecision(_reports).Should().BeEmpty();
        }

        [Test]
        public void should_not_aggregate_or_calculate_formats_for_an_unknown_series()
        {
            GivenSpecifications(_pass1);
            _remoteEpisode.Series = null;

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<IRemoteEpisodeAggregationService>().Verify(c => c.Augment(It.IsAny<RemoteEpisode>()), Times.Never());
            Mocker.GetMock<ITvCustomFormatCalculationService>().Verify(c => c.ParseCustomFormat(It.IsAny<RemoteEpisode>(), It.IsAny<long>()), Times.Never());
        }

        [Test]
        public void should_aggregate_and_calculate_formats_with_the_release_size_for_a_known_episode()
        {
            GivenSpecifications(_pass1);
            _reports[0].Size = 1234;

            var formats = new List<CustomFormat> { new CustomFormat("Format") { Id = 1 } };

            Mocker.GetMock<ITvCustomFormatCalculationService>()
                  .Setup(c => c.ParseCustomFormat(_remoteEpisode, 1234))
                  .Returns(formats);

            var decision = Subject.GetRssDecision(_reports).Single();

            Mocker.GetMock<IRemoteEpisodeAggregationService>().Verify(c => c.Augment(_remoteEpisode), Times.Once());
            decision.GetRemoteEpisode().CustomFormats.Should().BeSameAs(formats);
            decision.GetRemoteEpisode().DownloadAllowed.Should().BeTrue();
        }

        [Test]
        public void should_calculate_formats_with_size_zero_when_the_release_size_is_unknown()
        {
            GivenSpecifications(_pass1);
            _reports[0].Size = null;

            Subject.GetRssDecision(_reports);

            Mocker.GetMock<ITvCustomFormatCalculationService>().Verify(c => c.ParseCustomFormat(_remoteEpisode, 0), Times.Once());
        }

        [Test]
        public void should_set_the_custom_format_score_from_the_series_profile()
        {
            GivenSpecifications(_pass1);

            var format = new CustomFormat("Format") { Id = 1 };

            _remoteEpisode.Series.QualityProfile = new QualityProfile
            {
                FormatItems = new List<ProfileFormatItem> { new ProfileFormatItem { Format = format, Score = 25 } }
            };

            Mocker.GetMock<ITvCustomFormatCalculationService>()
                  .Setup(c => c.ParseCustomFormat(_remoteEpisode, It.IsAny<long>()))
                  .Returns(new List<CustomFormat> { format });

            var decision = Subject.GetRssDecision(_reports).Single();

            decision.GetRemoteEpisode().CustomFormatScore.Should().Be(25);
        }

        [TestCase(false, false, false, ReleaseSourceType.Rss)]
        [TestCase(true, false, false, ReleaseSourceType.ReleasePush)]
        public void should_set_release_source_for_rss_decisions(bool pushed, bool interactive, bool userInvoked, ReleaseSourceType expected)
        {
            GivenSpecifications(_pass1);

            var decision = Subject.GetRssDecision(_reports, pushed).Single();

            decision.GetRemoteEpisode().ReleaseSource.Should().Be(expected);
        }

        [TestCase(false, false, ReleaseSourceType.Search)]
        [TestCase(true, false, ReleaseSourceType.UserInvokedSearch)]
        [TestCase(false, true, ReleaseSourceType.InteractiveSearch)]
        [TestCase(true, true, ReleaseSourceType.InteractiveSearch)]
        public void should_set_release_source_for_search_decisions(bool userInvoked, bool interactive, ReleaseSourceType expected)
        {
            GivenSpecifications(_pass1);

            var request = new SingleEpisodeSearchRequest { UserInvokedSearch = userInvoked, InteractiveSearch = interactive };

            var decision = Subject.GetSearchDecision(_reports, request).Single();

            decision.GetRemoteEpisode().ReleaseSource.Should().Be(expected);
        }

        [Test]
        public void should_pass_the_search_request_and_push_flag_to_specifications()
        {
            ReleaseDecisionInformation seen = null;

            _pass1.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>()))
                  .Callback<RemoteEpisode, ReleaseDecisionInformation>((_, i) => seen = i)
                  .Returns(DownloadSpecDecision.Accept);

            GivenSpecifications(_pass1);

            var request = new SingleEpisodeSearchRequest();

            Subject.GetSearchDecision(_reports, request);

            seen.SearchCriteria.Should().BeSameAs(request);
            seen.PushedRelease.Should().BeFalse();

            Subject.GetRssDecision(_reports, true);

            seen.SearchCriteria.Should().BeNull();
            seen.PushedRelease.Should().BeTrue();
        }

        [Test]
        public void should_reject_with_decision_error_when_a_specification_throws()
        {
            var throwing = new Mock<IDownloadDecisionEngineSpecification>();
            throwing.Setup(c => c.IsSatisfiedBy(It.IsAny<RemoteEpisode>(), It.IsAny<ReleaseDecisionInformation>())).Throws(new System.InvalidOperationException("boom"));

            GivenSpecifications(throwing);

            var rejection = Subject.GetRssDecision(_reports).Single().Rejections.Single();

            rejection.Reason.Should().Be(DownloadRejectionReason.DecisionError);
            rejection.Message.Should().Contain("boom");

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_reject_with_error_when_the_release_cannot_be_processed()
        {
            GivenSpecifications(_pass1);

            Mocker.GetMock<IParsingService>()
                  .Setup(c => c.Map(It.IsAny<ParsedEpisodeInfo>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<TvSearchRequest>()))
                  .Throws<TestException>();

            var decision = Subject.GetRssDecision(_reports).Single();

            decision.Rejections.Single().Reason.Should().Be(DownloadRejectionReason.Error);
            decision.GetRemoteEpisode().Release.Should().BeSameAs(_reports[0]);

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_keep_the_parsed_quality_and_languages_on_an_unparsable_search_release()
        {
            GivenSpecifications(_pass1);
            _reports[0].Title = "1937 - Snow White and the Seven Dwarves German Bluray 1080p";

            var remote = Subject.GetSearchDecision(_reports, new SingleEpisodeSearchRequest()).Single().GetRemoteEpisode();

            remote.ParsedEpisodeInfo.Should().NotBeNull();
            remote.ParsedEpisodeInfo.Quality.Quality.Should().Be(QualityParser.ParseQuality(_reports[0].Title).Quality);
            remote.ParsedEpisodeInfo.Quality.Quality.Should().Be(NzbDrone.Core.Qualities.Quality.Bluray1080p);
            remote.Languages.Should().Equal(remote.ParsedEpisodeInfo.Languages);
            remote.Languages.Should().Contain(NzbDrone.Core.Languages.Language.German);
        }
    }
}
