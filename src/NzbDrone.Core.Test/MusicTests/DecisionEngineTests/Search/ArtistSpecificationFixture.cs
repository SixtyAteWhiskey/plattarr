using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.DecisionEngine.Specifications.Search;
using NzbDrone.Core.Music.Parser.Model;
using NzbDrone.Core.Music.Search;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MusicTests.DecisionEngineTests.Search
{
    [TestFixture]
    public class ArtistSpecificationFixture : TestBase<ArtistSpecification>
    {
        private Artist _artist1;
        private Artist _artist2;
        private RemoteAlbum _remoteAlbum = new RemoteAlbum();
        private MusicSearchRequest _searchCriteria = new AlbumSearchRequest();

        [SetUp]
        public void Setup()
        {
            _artist1 = Builder<Artist>.CreateNew().With(s => s.Id = 1).Build();
            _artist2 = Builder<Artist>.CreateNew().With(s => s.Id = 2).Build();

            _remoteAlbum.Artist = _artist1;
        }

        [Test]
        public void should_return_false_if_artist_doesnt_match()
        {
            _searchCriteria.Artist = _artist2;

            var rejection = Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, _searchCriteria));

            rejection.Accepted.Should().BeFalse();

            rejection.Reason.Should().Be(DownloadRejectionReason.WrongArtist);
        }

        [Test]
        public void should_return_true_when_artist_ids_match()
        {
            _searchCriteria.Artist = _artist1;

            Subject.IsSatisfiedBy(_remoteAlbum, new ReleaseDecisionInformation(false, _searchCriteria)).Accepted.Should().BeTrue();
        }
    }
}
