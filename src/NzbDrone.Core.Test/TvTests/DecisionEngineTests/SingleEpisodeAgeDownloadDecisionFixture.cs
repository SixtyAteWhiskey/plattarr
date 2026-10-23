using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DecisionEngine.Specifications;
using NzbDrone.Core.Tv.Parser.Model;
using NzbDrone.Core.Tv.Search;

namespace NzbDrone.Core.Test.TvTests.DecisionEngineTests
{
    [TestFixture]
    public class SingleEpisodeAgeDownloadDecisionFixture : CoreTest<SeasonPackOnlySpecification>
    {
        private RemoteEpisode _parseResultMulti;
        private RemoteEpisode _parseResultSingle;
        private Series _series;
        private List<Episode> _episodes;
        private SeasonSearchRequest _multiSearch;
        private ReleaseDecisionInformation _multiInfo;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                    .With(s => s.Seasons = Builder<Season>.CreateListOfSize(1).Build().ToList())
                                    .With(s => s.SeriesType = SeriesTypes.Standard)
                                    .Build();

            _episodes = new List<Episode>();
            _episodes.Add(CreateEpisodeStub(1, 400));
            _episodes.Add(CreateEpisodeStub(2, 370));
            _episodes.Add(CreateEpisodeStub(3, 340));
            _episodes.Add(CreateEpisodeStub(4, 310));

            _multiSearch = new SeasonSearchRequest();
            _multiSearch.Episodes = _episodes.ToList();
            _multiSearch.SeasonNumber = 1;
            _multiInfo = new ReleaseDecisionInformation(false, _multiSearch);

            _parseResultMulti = new RemoteEpisode
            {
                Series = _series,
                Release = new ReleaseInfo(),
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.SDTV, new Revision(version: 2)), FullSeason = true },
                Episodes = _episodes.ToList()
            };

            _parseResultSingle = new RemoteEpisode
            {
                Series = _series,
                Release = new ReleaseInfo(),
                ParsedEpisodeInfo = new ParsedEpisodeInfo { Quality = new QualityModel(Quality.SDTV, new Revision(version: 2)) },
                Episodes = new List<Episode>()
            };
        }

        private Episode CreateEpisodeStub(int number, int age)
        {
            return new Episode()
            {
                SeasonNumber = 1,
                EpisodeNumber = number,
                AirDateUtc = DateTime.UtcNow.AddDays(-age)
            };
        }

        private void GivenIndexerMaximumSingleEpisodeAge(int days)
        {
            // Sonarr stamps the setting on the release; here it is the indexer's TV override.
            Mocker.GetMock<IIndexerOverrideService>()
                  .Setup(s => s.Get(It.IsAny<int>(), MediaKind.Tv))
                  .Returns(new IndexerOverride { MediaKind = MediaKind.Tv, SeasonSearchMaximumSingleEpisodeAge = days });
        }

        [TestCase(1, 200, false)]
        [TestCase(4, 200, false)]
        [TestCase(1, 600, true)]
        [TestCase(1, 365, true)]
        [TestCase(4, 365, true)]
        [TestCase(1, 0, true)]
        public void single_episode_release(int episode, int seasonSearchMaximumSingleEpisodeAge, bool expectedResult)
        {
            GivenIndexerMaximumSingleEpisodeAge(seasonSearchMaximumSingleEpisodeAge);
            _parseResultSingle.Episodes.Clear();
            _parseResultSingle.Episodes.Add(_episodes.Find(e => e.EpisodeNumber == episode));

            Subject.IsSatisfiedBy(_parseResultSingle, _multiInfo).Accepted.Should().Be(expectedResult);
        }

        // should always accept all season packs
        [TestCase(200, true)]
        [TestCase(600, true)]
        [TestCase(365, true)]
        [TestCase(0, true)]
        public void multi_episode_release(int seasonSearchMaximumSingleEpisodeAge, bool expectedResult)
        {
            GivenIndexerMaximumSingleEpisodeAge(seasonSearchMaximumSingleEpisodeAge);

            Subject.IsSatisfiedBy(_parseResultMulti, _multiInfo).Accepted.Should().BeTrue();
        }
    }
}
