using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Pending;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.DownloadApprovedReportsTests
{
    [TestFixture]
    public class DownloadApprovedFixture : CoreTest<ProcessDownloadDecisions>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IPrioritizeDownloadDecision>()
                .Setup(v => v.PrioritizeDecisions(It.IsAny<List<DownloadDecision>>()))
                .Returns<List<DownloadDecision>>(v => v);

            Mocker.GetMock<IPendingReleaseHandler>()
                .SetupGet(v => v.Kind)
                .Returns(MediaKind.Movies);

            Mocker.SetConstant<IEnumerable<IPendingReleaseHandler>>(new[] { Mocker.GetMock<IPendingReleaseHandler>().Object });
        }

        private Movie GetMovie(int id)
        {
            return Builder<Movie>.CreateNew()
                            .With(e => e.Id = id)
                                 .With(m => m.Tags = new HashSet<int>())

                            .Build();
        }

        private RemoteMovie GetRemoteMovie(QualityModel quality, Movie movie = null, DownloadProtocol downloadProtocol = DownloadProtocol.Usenet)
        {
            if (movie == null)
            {
                movie = GetMovie(1);
            }

            movie.QualityProfile = new QualityProfile { Items = Qualities.QualityFixture.GetDefaultQualities() };

            var remoteMovie = new RemoteMovie()
            {
                ParsedMovieInfo = new ParsedMovieInfo()
                {
                    Quality = quality,
                    Year = 1998,
                    MovieTitles = new List<string> { "A Movie" },
                },
                Movie = movie,

                Release = new ReleaseInfo()
                {
                    PublishDate = DateTime.UtcNow,
                    Title = "A.Movie.1998",
                    Size = 200,
                    DownloadProtocol = downloadProtocol
                }
            };

            return remoteMovie;
        }

        [Test]
        public async Task should_download_report_if_movie_was_not_already_downloaded()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteMovie>(), null), Times.Once());
        }

        [Test]
        public async Task should_only_download_movie_once()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));
            decisions.Add(new DownloadDecision(remoteMovie));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteMovie>(), null), Times.Once());
        }

        [Test]
        public async Task should_not_download_if_any_movie_was_already_downloaded()
        {
            var remoteMovie1 = GetRemoteMovie(
                                                    new QualityModel(Quality.HDTV720p));

            var remoteMovie2 = GetRemoteMovie(
                                                    new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie1));
            decisions.Add(new DownloadDecision(remoteMovie2));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteMovie>(), null), Times.Once());
        }

        [Test]
        public async Task should_return_downloaded_reports()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().HaveCount(1);
        }

        [Test]
        public async Task should_return_all_downloaded_reports()
        {
            var remoteMovie1 = GetRemoteMovie(new QualityModel(Quality.HDTV720p), GetMovie(1));

            var remoteMovie2 = GetRemoteMovie(new QualityModel(Quality.HDTV720p), GetMovie(2));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie1));
            decisions.Add(new DownloadDecision(remoteMovie2));

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().HaveCount(2);
        }

        [Test]
        public async Task should_only_return_downloaded_reports()
        {
            var remoteMovie1 = GetRemoteMovie(
                                                    new QualityModel(Quality.HDTV720p),
                                                    GetMovie(1));

            var remoteMovie2 = GetRemoteMovie(
                                                    new QualityModel(Quality.HDTV720p),
                                                    GetMovie(2));

            var remoteMovie3 = GetRemoteMovie(
                                                    new QualityModel(Quality.HDTV720p),
                                                    GetMovie(2));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie1));
            decisions.Add(new DownloadDecision(remoteMovie2));
            decisions.Add(new DownloadDecision(remoteMovie3));

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().HaveCount(2);
        }

        [Test]
        public async Task should_not_add_to_downloaded_list_when_download_fails()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));

            Mocker.GetMock<IDownloadService>().Setup(s => s.DownloadReport(It.IsAny<RemoteMovie>(), null)).Throws(new Exception());

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().BeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_return_an_empty_list_when_none_are_appproved()
        {
            var decisions = new List<DownloadDecision>();
            RemoteMovie remoteMovie = null;
            decisions.Add(new DownloadDecision(remoteMovie, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!")));
            decisions.Add(new DownloadDecision(remoteMovie, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!")));

            Subject.GetQualifiedReports(decisions).Should().BeEmpty();
        }

        [Test]
        public async Task should_not_grab_if_pending()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!", RejectionType.Temporary)));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteMovie>(), null), Times.Never());
        }

        [Test]
        public async Task should_not_add_to_pending_if_movie_was_grabbed()
        {
            var removeMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(removeMovie));
            decisions.Add(new DownloadDecision(removeMovie, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!", RejectionType.Temporary)));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IPendingReleaseHandler>().Verify(v => v.AddMany(It.IsAny<List<Tuple<DownloadDecision, PendingReleaseReason>>>()), Times.Never());
        }

        [Test]
        public async Task should_add_to_pending_even_if_already_added_to_pending()
        {
            var remoteEpisode = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!", RejectionType.Temporary)));
            decisions.Add(new DownloadDecision(remoteEpisode, new DownloadRejection(DownloadRejectionReason.Unknown, "Failure!", RejectionType.Temporary)));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IPendingReleaseHandler>().Verify(v => v.AddMany(It.IsAny<List<Tuple<DownloadDecision, PendingReleaseReason>>>()), Times.Once());
        }

        [Test]
        public async Task should_add_to_failed_if_already_failed_for_that_protocol()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));
            decisions.Add(new DownloadDecision(remoteMovie));

            Mocker.GetMock<IDownloadService>().Setup(s => s.DownloadReport(It.IsAny<RemoteMovie>(), null))
                  .Throws(new DownloadClientUnavailableException("Download client failed"));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteMovie>(), null), Times.Once());
        }

        [Test]
        public async Task should_not_add_to_failed_if_failed_for_a_different_protocol()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p), null, DownloadProtocol.Usenet);
            var remoteMovie2 = GetRemoteMovie(new QualityModel(Quality.HDTV720p), null, DownloadProtocol.Torrent);

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));
            decisions.Add(new DownloadDecision(remoteMovie2));

            Mocker.GetMock<IDownloadService>().Setup(s => s.DownloadReport(It.Is<RemoteMovie>(r => r.Release.DownloadProtocol == DownloadProtocol.Usenet), null))
                  .Throws(new DownloadClientUnavailableException("Download client failed"));

            await Subject.ProcessDecisions(decisions);
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.Is<RemoteMovie>(r => r.Release.DownloadProtocol == DownloadProtocol.Usenet), null), Times.Once());
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.Is<RemoteMovie>(r => r.Release.DownloadProtocol == DownloadProtocol.Torrent), null), Times.Once());
        }

        [Test]
        public async Task should_add_to_rejected_if_release_unavailable_on_indexer()
        {
            var remoteMovie = GetRemoteMovie(new QualityModel(Quality.HDTV720p));

            var decisions = new List<DownloadDecision>();
            decisions.Add(new DownloadDecision(remoteMovie));

            Mocker.GetMock<IDownloadService>()
                  .Setup(s => s.DownloadReport(It.IsAny<RemoteMovie>(), null))
                  .Throws(new ReleaseUnavailableException(remoteMovie.Release, "That 404 Error is not just a Quirk"));

            var result = await Subject.ProcessDecisions(decisions);

            result.Grabbed.Should().BeEmpty();
            result.Rejected.Should().NotBeEmpty();

            ExceptionVerification.ExpectedWarns(1);
        }

        private static TvRemoteRelease GetTvRelease(int seriesId, int episodeId)
        {
            return new TvRemoteRelease
            {
                Series = new TvParent { Id = seriesId, Title = "A Series" },
                Items = new[] { episodeId },
                Release = new ReleaseInfo
                {
                    PublishDate = DateTime.UtcNow,
                    Title = "A.Series.S01E01",
                    Size = 200,
                    DownloadProtocol = DownloadProtocol.Usenet
                }
            };
        }

        [Test]
        public async Task should_download_a_release_from_another_media_type()
        {
            var release = GetTvRelease(7, 70);

            var result = await Subject.ProcessDecisions(new List<DownloadDecision> { new DownloadDecision(release) });

            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(release, null), Times.Once());
            result.Grabbed.Should().HaveCount(1);
        }

        [Test]
        public async Task should_not_treat_the_same_item_id_of_another_media_type_as_already_downloaded()
        {
            var movie = GetRemoteMovie(new QualityModel(Quality.HDTV720p), GetMovie(5));
            var tv = GetTvRelease(5, 5);

            var result = await Subject.ProcessDecisions(new List<DownloadDecision> { new DownloadDecision(movie), new DownloadDecision(tv) });

            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(movie, null), Times.Once());
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(tv, null), Times.Once());
            result.Grabbed.Should().HaveCount(2);
        }

        [Test]
        public async Task should_only_download_an_item_of_another_media_type_once()
        {
            var first = GetTvRelease(7, 70);
            var second = GetTvRelease(7, 70);

            await Subject.ProcessDecisions(new List<DownloadDecision> { new DownloadDecision(first), new DownloadDecision(second) });

            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteRelease>(), null), Times.Once());
        }

        [Test]
        public async Task should_hand_a_delayed_release_to_the_pending_handler_of_its_media_type()
        {
            var movieHandler = new Mock<IPendingReleaseHandler>();
            movieHandler.SetupGet(h => h.Kind).Returns(MediaKind.Movies);

            var tvHandler = new Mock<IPendingReleaseHandler>();
            tvHandler.SetupGet(h => h.Kind).Returns(MediaKind.Tv);

            Mocker.SetConstant<IEnumerable<IPendingReleaseHandler>>(new[] { movieHandler.Object, tvHandler.Object });

            var delayed = new DownloadDecision(GetTvRelease(7, 70), new DownloadRejection(DownloadRejectionReason.MinimumAge, "Delayed", RejectionType.Temporary));

            await Subject.ProcessDecisions(new List<DownloadDecision> { delayed });

            tvHandler.Verify(h => h.AddMany(It.Is<List<Tuple<DownloadDecision, PendingReleaseReason>>>(l => l.Count == 1 && l[0].Item1 == delayed)), Times.Once());
            movieHandler.Verify(h => h.AddMany(It.IsAny<List<Tuple<DownloadDecision, PendingReleaseReason>>>()), Times.Never());
            Mocker.GetMock<IDownloadService>().Verify(v => v.DownloadReport(It.IsAny<RemoteRelease>(), It.IsAny<int?>()), Times.Never());
        }

        [Test]
        public async Task should_fail_loudly_when_no_pending_handler_exists_for_the_media_type()
        {
            var delayed = new DownloadDecision(GetTvRelease(7, 70), new DownloadRejection(DownloadRejectionReason.MinimumAge, "Delayed", RejectionType.Temporary));

            Func<Task> process = () => Subject.ProcessDecisions(new List<DownloadDecision> { delayed });

            await process.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Tv*");
        }
    }
}
