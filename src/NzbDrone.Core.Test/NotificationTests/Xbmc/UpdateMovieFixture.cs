using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Notifications.Xbmc;
using NzbDrone.Core.Notifications.Xbmc.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.NotificationTests.Xbmc
{
    [TestFixture]
    public class UpdateMovieFixture : CoreTest<XbmcService>
    {
        private const string IMDB_ID = "tt67890";
        private XbmcSettings _settings;
        private List<XbmcMovie> _xbmcMovies;

        [SetUp]
        public void Setup()
        {
            _settings = Builder<XbmcSettings>.CreateNew()
                                             .Build();

            _xbmcMovies = Builder<XbmcMovie>.CreateListOfSize(3)
                                            .TheFirst(1)
                                            .With(s => s.ImdbNumber = IMDB_ID)
                                            .Build()
                                            .ToList();

            Mocker.GetMock<IXbmcJsonApiProxy>()
                  .Setup(s => s.GetMovies(_settings))
                  .Returns(_xbmcMovies);

            Mocker.GetMock<IXbmcJsonApiProxy>()
                  .Setup(s => s.GetActivePlayers(_settings))
                  .Returns(new List<ActivePlayer>());
        }

        [Test]
        public void should_update_using_movie_path()
        {
            var movie = new ActivitySubject
            {
                Kind = MediaKind.Movies,
                ExternalIds = new() { [ExternalIdTypes.Imdb] = IMDB_ID }
            };

            Subject.Update(_settings, movie);

            Mocker.GetMock<IXbmcJsonApiProxy>()
                  .Verify(v => v.UpdateLibrary(_settings, It.IsAny<string>()), Times.Once());
        }

        [Test]
        public void should_update_all_paths_when_movie_path_not_found()
        {
            var fakeMovie = new ActivitySubject
            {
                Kind = MediaKind.Movies,
                ParentTitle = "Not A Real Movie",
                ExternalIds = new() { [ExternalIdTypes.Imdb] = "tt01000" }
            };

            Subject.Update(_settings, fakeMovie);

            Mocker.GetMock<IXbmcJsonApiProxy>()
                  .Verify(v => v.UpdateLibrary(_settings, null), Times.Once());
        }
    }
}
