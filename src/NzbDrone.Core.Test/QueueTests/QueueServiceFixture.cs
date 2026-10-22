using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Queue;
using NzbDrone.Core.Test.DecisionEngineTests.ReleaseSpecifications;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class QueueServiceFixture : CoreTest<QueueService>
    {
        private List<TrackedDownload> _trackedDownloads;
        private RemoteMovie _remoteMovie;
        private Movie _movie;

        [SetUp]
        public void SetUp()
        {
            var downloadClientInfo = Builder<DownloadClientItemClientInfo>.CreateNew().Build();

            var downloadItem = Builder<NzbDrone.Core.Download.DownloadClientItem>.CreateNew()
                                        .With(v => v.RemainingTime = TimeSpan.FromSeconds(10))
                                        .With(v => v.DownloadClientInfo = downloadClientInfo)
                                        .Build();

            _movie = Builder<Movie>.CreateNew()
                                   .Build();

            _remoteMovie = Builder<RemoteMovie>.CreateNew()
                                               .With(r => r.Movie = _movie)
                                               .With(r => r.ParsedMovieInfo = new ParsedMovieInfo { Quality = new QualityModel(Quality.Bluray1080p) })
                                               .Build();

            _trackedDownloads = Builder<TrackedDownload>.CreateListOfSize(1)
                .All()
                .With(v => v.IsTrackable = true)
                .With(v => v.DownloadItem = downloadItem)
                .With(v => v.RemoteMovie = _remoteMovie)
                .Build()
                .ToList();
        }

        [Test]
        public void queue_items_should_have_id()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue();

            queue.Should().HaveCount(1);

            queue.All(v => v.Id > 0).Should().BeTrue();

            var distinct = queue.Select(v => v.Id).Distinct().ToArray();

            distinct.Should().HaveCount(1);
        }

        [Test]
        public void should_carry_the_movie_match_as_a_release()
        {
            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeSameAs(_remoteMovie);
            queue.GetRemoteMovie().Should().BeSameAs(_remoteMovie);
            queue.GetMovie().Should().BeSameAs(_movie);
            queue.Quality.Quality.Should().Be(Quality.Bluray1080p);
            queue.Languages.Should().BeSameAs(_remoteMovie.Languages);
        }

        [Test]
        public void should_map_a_download_matched_by_another_module_without_any_movie()
        {
            var language = new List<Language> { Language.French };
            var release = new TvRemoteRelease
            {
                Languages = language,
                ParsedQuality = new QualityModel(Quality.HDTV720p)
            };

            _trackedDownloads[0].RemoteRelease = release;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeSameAs(release);
            queue.Quality.Quality.Should().Be(Quality.HDTV720p);
            queue.Languages.Should().BeSameAs(language);
            queue.GetRemoteMovie().Should().BeNull();
            queue.GetMovie().Should().BeNull();
        }

        [Test]
        public void should_use_unknown_quality_and_language_for_an_unmatched_download()
        {
            _trackedDownloads[0].RemoteRelease = null;

            Subject.Handle(new TrackedDownloadRefreshedEvent(_trackedDownloads));

            var queue = Subject.GetQueue().Single();

            queue.RemoteRelease.Should().BeNull();
            queue.Quality.Quality.Should().Be(Quality.Unknown);
            queue.Languages.Should().BeEquivalentTo(new[] { Language.Unknown });
            queue.GetMovie().Should().BeNull();
        }
    }
}
