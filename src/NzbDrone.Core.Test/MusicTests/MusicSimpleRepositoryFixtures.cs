using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Organizer;
using NzbDrone.Core.Music.RootFolders;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class MusicRootFolderRepositoryFixture : DbTest<RootFolderRepository, RootFolder>
    {
        [Test]
        public void should_round_trip_every_column_and_ignore_the_disk_properties()
        {
            var folder = new RootFolder
            {
                Name = "Music",
                Path = "/data/music",
                DefaultMetadataProfileId = 2,
                DefaultQualityProfileId = 3,
                DefaultMonitorOption = MonitorTypes.Future,
                DefaultNewItemMonitorOption = NewItemMonitorTypes.New,
                DefaultTags = new HashSet<int> { 4, 6 },
                Accessible = true,
                FreeSpace = 1,
                TotalSpace = 2
            };

            Subject.Insert(folder);

            var stored = Subject.Get(folder.Id);

            stored.Name.Should().Be("Music");
            stored.Path.Should().Be("/data/music");
            stored.DefaultMetadataProfileId.Should().Be(2);
            stored.DefaultQualityProfileId.Should().Be(3);
            stored.DefaultMonitorOption.Should().Be(MonitorTypes.Future);
            stored.DefaultNewItemMonitorOption.Should().Be(NewItemMonitorTypes.New);
            stored.DefaultTags.Should().BeEquivalentTo(new[] { 4, 6 });
            stored.Accessible.Should().BeFalse("the disk state is computed, never stored");
            stored.FreeSpace.Should().BeNull();
        }

        [Test]
        public void should_keep_paths_unique()
        {
            Subject.Insert(new RootFolder { Path = "/data/music", DefaultTags = new HashSet<int>() });

            System.Action insert = () => Subject.Insert(new RootFolder { Path = "/data/music", DefaultTags = new HashSet<int>() });

            insert.Should().Throw<System.Exception>();
        }
    }

    [TestFixture]
    public class MusicNamingConfigRepositoryFixture : DbTest<NamingConfigRepository, NamingConfig>
    {
        [Test]
        public void should_round_trip_the_default_config()
        {
            var config = NamingConfig.Default;

            Subject.Insert(config);

            var stored = Subject.Get(config.Id);

            stored.Should().BeEquivalentTo(config);
            stored.ColonReplacementFormat.Should().Be(ColonReplacementFormat.Smart);
            stored.StandardTrackFormat.Should().Contain("{Album Title}");
        }

        [Test]
        public void should_round_trip_a_changed_config()
        {
            var config = NamingConfig.Default;
            config.RenameTracks = true;
            config.ReplaceIllegalCharacters = false;
            config.ColonReplacementFormat = ColonReplacementFormat.Dash;
            config.ArtistFolderFormat = "{Artist Name} ({Artist MbId})";

            Subject.Insert(config);

            Subject.Get(config.Id).Should().BeEquivalentTo(config);
        }
    }
}
