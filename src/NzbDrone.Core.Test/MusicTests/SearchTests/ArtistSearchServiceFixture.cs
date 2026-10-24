using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Music.Search;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.SearchTests
{
    [TestFixture]
    public class ArtistSearchServiceFixture : CoreTest<ArtistSearchService>
    {
        private List<DownloadDecision> _decisions;

        [SetUp]
        public void Setup()
        {
            _decisions = new List<DownloadDecision>();

            Mocker.GetMock<ISearchForReleases>()
                .Setup(s => s.ArtistSearch(7, false, It.IsAny<bool>(), false))
                .ReturnsAsync(_decisions);

            Mocker.GetMock<IProcessDownloadDecisions>()
                .Setup(s => s.ProcessDecisions(_decisions))
                .ReturnsAsync(new ProcessedDecisions(new List<DownloadDecision>(), new List<DownloadDecision>(), new List<DownloadDecision>()));
        }

        [Test]
        public void should_search_the_artist_once_and_process_the_decisions()
        {
            Subject.Execute(new ArtistSearchCommand { ArtistId = 7, Trigger = CommandTrigger.Manual });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.ArtistSearch(7, false, true, false), Times.Once());
            Mocker.GetMock<IProcessDownloadDecisions>().Verify(v => v.ProcessDecisions(_decisions), Times.Once());
        }

        [Test]
        public void should_not_mark_a_scheduled_search_as_user_invoked()
        {
            Subject.Execute(new ArtistSearchCommand { ArtistId = 7, Trigger = CommandTrigger.Unspecified });

            Mocker.GetMock<ISearchForReleases>().Verify(v => v.ArtistSearch(7, false, false, false), Times.Once());
        }
    }
}
