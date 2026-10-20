using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class RequiredIndexerFlagsSpecificationFixture : CoreTest<RequiredIndexerFlagsSpecification>
    {
        private RemoteMovie _remoteMovie;
        private IndexerOverride _override;

        [SetUp]
        public void Setup()
        {
            _override = new IndexerOverride { IndexerId = 1, MediaKind = MediaKind.Movies };

            Mocker.GetMock<IIndexerOverrideService>()
                  .Setup(s => s.Get(1, MediaKind.Movies))
                  .Returns(() => _override);

            _remoteMovie = new RemoteMovie
            {
                Release = new TorrentInfo { IndexerId = 1, Title = "Movie.2024.1080p.WEB-DL-Grp" }
            };
        }

        [Test]
        public void should_accept_when_no_flags_required()
        {
            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_when_required_flag_missing()
        {
            _override.RequiredFlags = new List<int> { (int)IndexerFlags.G_Freeleech };

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_any_required_flag_present()
        {
            _override.RequiredFlags = new List<int> { (int)IndexerFlags.G_Freeleech, (int)IndexerFlags.G_Internal };
            ((TorrentInfo)_remoteMovie.Release).IndexerFlags.Add(IndexerFlag.Internal);

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_ignore_usenet_releases()
        {
            _override.RequiredFlags = new List<int> { (int)IndexerFlags.G_Freeleech };
            _remoteMovie.Release = new ReleaseInfo { IndexerId = 1 };

            Subject.IsSatisfiedBy(_remoteMovie, new()).Accepted.Should().BeTrue();
        }
    }
}
