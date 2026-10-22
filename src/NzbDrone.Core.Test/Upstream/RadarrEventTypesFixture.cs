using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.History;
using NzbDrone.Core.History;
using NzbDrone.Core.Movies.Upstream;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Upstream
{
    // The cases are Radarr's own enums at develop 56f6168 (MovieHistoryEventType in
    // History/History.cs, DownloadHistoryEventType in Download/History/DownloadHistory.cs),
    // not the map under test. The owner's database uses History 1, 3, 4, 6 and
    // DownloadHistory 1, 2, 3, 5.
    [TestFixture]
    public class RadarrEventTypesFixture : CoreTest
    {
        private static readonly object[] RadarrHistory =
        {
            new object[] { 0, "Unknown" },
            new object[] { 1, "Grabbed" },
            new object[] { 3, "DownloadFolderImported" },
            new object[] { 4, "DownloadFailed" },
            new object[] { 6, "MovieFileDeleted" },
            new object[] { 7, "MovieFolderImported" },
            new object[] { 8, "MovieFileRenamed" },
            new object[] { 9, "DownloadIgnored" },
        };

        private static readonly object[] RadarrDownloadHistory =
        {
            new object[] { 1, "DownloadGrabbed" },
            new object[] { 2, "DownloadImported" },
            new object[] { 3, "DownloadFailed" },
            new object[] { 4, "DownloadIgnored" },
            new object[] { 5, "FileImported" },
        };

        [TestCaseSource(nameof(RadarrHistory))]
        public void history_event_should_map_to_the_member_with_the_same_name(int radarrValue, string name)
        {
            RadarrEventTypes.TryMapHistory(radarrValue, out var type).Should().BeTrue();

            type.ToString().Should().Be(name);
        }

        [TestCaseSource(nameof(RadarrDownloadHistory))]
        public void download_history_event_should_map_to_the_member_with_the_same_name(int radarrValue, string name)
        {
            RadarrEventTypes.TryMapDownloadHistory(radarrValue, out var type).Should().BeTrue();

            type.ToString().Should().Be(name);
        }

        [TestCase(2)]
        [TestCase(5)]
        [TestCase(10)]
        [TestCase(-1)]
        public void should_not_map_a_history_value_radarr_never_had(int value)
        {
            RadarrEventTypes.TryMapHistory(value, out _).Should().BeFalse();
        }

        [TestCase(0)]
        [TestCase(6)]
        [TestCase(-1)]
        public void should_not_map_a_download_history_value_radarr_never_had(int value)
        {
            RadarrEventTypes.TryMapDownloadHistory(value, out _).Should().BeFalse();
        }

        // A member added to either enum without a map entry would be unreachable from an import.
        [Test]
        public void every_plattarr_event_type_should_be_the_target_of_exactly_one_radarr_value()
        {
            RadarrEventTypes.History.Values.Should().BeEquivalentTo(Enum.GetValues<MovieHistoryEventType>());
            RadarrEventTypes.Downloads.Values.Should().BeEquivalentTo(Enum.GetValues<DownloadHistoryEventType>());

            RadarrEventTypes.History.Values.Distinct().Count().Should().Be(RadarrEventTypes.History.Count);
            RadarrEventTypes.Downloads.Values.Distinct().Count().Should().Be(RadarrEventTypes.Downloads.Count);
        }

        [Test]
        public void map_should_cover_every_radarr_value_and_nothing_else()
        {
            RadarrEventTypes.History.Keys.Should().BeEquivalentTo(RadarrHistory.Cast<object[]>().Select(c => (int)c[0]));
            RadarrEventTypes.Downloads.Keys.Should().BeEquivalentTo(RadarrDownloadHistory.Cast<object[]>().Select(c => (int)c[0]));
        }
    }
}
