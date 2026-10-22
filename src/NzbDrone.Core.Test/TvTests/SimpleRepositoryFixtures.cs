using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Datastore.Events;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.DataAugmentation.Scene;
using NzbDrone.Core.Tv.Organizer;
using NzbDrone.Core.Tv.RootFolders;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SceneMappingRepositoryFixture : DbTest<SceneMappingRepository, SceneMapping>
    {
        [Test]
        public void should_round_trip_every_column_including_the_search_mode_flags()
        {
            var mapping = new SceneMapping
            {
                MappingId = "m-1",
                Title = "Breaking Bad",
                ParseTerm = "breakingbad",
                SearchTerm = "Breaking Bad",
                TvdbId = 81189,
                SeasonNumber = 2,
                SceneSeasonNumber = 3,
                SceneOrigin = "tvdb",
                SearchMode = SearchMode.SearchID | SearchMode.SearchTitle,
                Comment = "note",
                FilterRegex = "^a",
                Type = "ServicesProvider"
            };

            Subject.Insert(mapping);

            Subject.Get(mapping.Id).Should().BeEquivalentTo(mapping);
        }

        [Test]
        public void should_keep_nullable_columns_null()
        {
            Subject.Insert(new SceneMapping { TvdbId = 5, Title = "t" });

            var stored = Subject.All().Single();

            stored.SeasonNumber.Should().BeNull();
            stored.SceneSeasonNumber.Should().BeNull();
            stored.SearchMode.Should().BeNull();
        }

        [Test]
        public void should_find_by_tvdb_id_and_type()
        {
            Subject.Insert(new SceneMapping { TvdbId = 1, Title = "a", Type = "x" });
            Subject.Insert(new SceneMapping { TvdbId = 1, Title = "b", Type = "y" });
            Subject.Insert(new SceneMapping { TvdbId = 2, Title = "c", Type = "x" });

            Subject.FindByTvdbid(1).Should().HaveCount(2);
            Subject.GetAllByType("x").Select(m => m.Title).Should().BeEquivalentTo(new[] { "a", "c" });
        }
    }

    [TestFixture]
    public class NamingConfigRepositoryFixture : DbTest<NamingConfigRepository, NamingConfig>
    {
        [Test]
        public void should_round_trip_the_default_config_and_every_enum()
        {
            var config = NamingConfig.Default;
            config.RenameEpisodes = true;
            config.ColonReplacementFormat = ColonReplacementFormat.Custom;
            config.CustomColonReplacementFormat = "~";
            config.MultiEpisodeStyle = MultiEpisodeStyle.Scene;

            Subject.Insert(config);

            Subject.Get(config.Id).Should().BeEquivalentTo(config);
        }
    }

    [TestFixture]
    public class RootFolderRepositoryFixture : DbTest<RootFolderRepository, RootFolder>
    {
        [Test]
        public void should_store_only_the_path_and_publish_model_events()
        {
            var folder = new RootFolder { Path = "/data/tv", Accessible = true, FreeSpace = 5, UnmappedFolders = new List<UnmappedFolder>() };

            Subject.Insert(folder);

            var stored = Subject.Get(folder.Id);
            stored.Path.Should().Be("/data/tv");
            stored.Accessible.Should().BeFalse("accessibility is computed, never stored");
            stored.FreeSpace.Should().BeNull();

            Mocker.GetMock<IEventAggregator>().Verify(
                e => e.PublishEvent(It.Is<ModelEvent<RootFolder>>(m => m.Action == ModelAction.Created)),
                Times.Once());
        }

        [Test]
        public void should_refuse_the_same_path_twice()
        {
            Subject.Insert(new RootFolder { Path = "/data/tv" });

            var action = () => Subject.Insert(new RootFolder { Path = "/data/tv" });

            action.Should().Throw<System.Exception>();
        }
    }

    [TestFixture]
    public class SeriesTranslationRepositoryFixture : DbTest<SeriesTranslationRepository, SeriesTranslation>
    {
        [Test]
        public void should_round_trip_a_translation_and_list_by_series()
        {
            Subject.Insert(new SeriesTranslation { SeriesId = 1, Language = Language.French, Title = "Titre", CleanTitle = "titre", Overview = "Résumé" });
            Subject.Insert(new SeriesTranslation { SeriesId = 1, Language = Language.German, Title = "Titel", CleanTitle = "titel", Overview = "Zusammenfassung" });
            Subject.Insert(new SeriesTranslation { SeriesId = 2, Language = Language.French, Title = "Autre", CleanTitle = "autre", Overview = "x" });

            var forOne = Subject.GetTranslations(1);

            forOne.Should().HaveCount(2);
            forOne.Single(t => t.Language == Language.French).Overview.Should().Be("Résumé");
        }

        [Test]
        public void should_delete_translations_for_the_given_series_only()
        {
            Subject.Insert(new SeriesTranslation { SeriesId = 1, Language = Language.French, Title = "a" });
            Subject.Insert(new SeriesTranslation { SeriesId = 2, Language = Language.French, Title = "b" });

            Subject.DeleteTranslations(new List<int> { 1 });

            Subject.All().Single().SeriesId.Should().Be(2);
        }
    }
}
