using System.Collections.Generic;
using System.Linq;
using Dapper;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Lifecycle;
using NzbDrone.Core.Music.Qualities;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AudioQualityDefinitionServiceFixture : CoreTest<AudioQualityDefinitionService>
    {
        [Test]
        public void init_should_add_all_definitions()
        {
            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<AudioQualityDefinition>>(d => d.Count == AudioQuality.All.Count)), Times.Once());
        }

        [Test]
        public void init_should_insert_any_missing_definitions()
        {
            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<AudioQualityDefinition>
                      {
                          new AudioQualityDefinition(AudioQuality.MP3_192) { Weight = 1, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                .Verify(v => v.InsertMany(It.Is<List<AudioQualityDefinition>>(d => d.Count == AudioQuality.All.Count - 1)), Times.Once());
        }

        [Test]
        public void init_should_update_existing_definitions()
        {
            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<AudioQualityDefinition>
                      {
                          new AudioQualityDefinition(AudioQuality.MP3_192) { Weight = 1, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                .Verify(v => v.UpdateMany(It.Is<List<AudioQualityDefinition>>(d => d.Count == 1)), Times.Once());
        }

        [Test]
        public void init_should_remove_old_definitions()
        {
            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<AudioQualityDefinition>
                      {
                          new AudioQualityDefinition(new AudioQuality { Id = 399, Name = "Test" }) { Weight = 1, MinSize = 0, MaxSize = 100, Id = 20 }
                      });

            Subject.Handle(new ApplicationStartedEvent());

            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                .Verify(v => v.DeleteMany(It.Is<List<AudioQualityDefinition>>(d => d.Count == 1)), Times.Once());
        }

        [Test]
        public void should_give_each_definition_its_default_weight_and_group()
        {
            var flac = AudioQuality.DefaultQualityDefinitions.Single(d => d.Quality == AudioQuality.FLAC);
            Mocker.GetMock<IAudioQualityDefinitionRepository>()
                  .Setup(s => s.All())
                  .Returns(new List<AudioQualityDefinition> { new AudioQualityDefinition(AudioQuality.FLAC) { Id = 7, MinSize = 1 } });

            var definition = Subject.Get(AudioQuality.FLAC);

            definition.Weight.Should().Be(flac.Weight);
            definition.GroupName.Should().Be(flac.GroupName);
            definition.GroupWeight.Should().Be(flac.GroupWeight);
            definition.MinSize.Should().Be(1);
        }
    }

    [TestFixture]
    public class AudioQualityDefinitionRepositoryFixture : DbTest<AudioQualityDefinitionRepository, AudioQualityDefinition>
    {
        [Test]
        public void should_store_every_default_definition_by_catalog_id_and_read_it_back()
        {
            Subject.InsertMany(AudioQuality.DefaultQualityDefinitions.ToList());

            var stored = Subject.All();

            stored.Select(d => d.Quality).Should().BeEquivalentTo(AudioQuality.All);

            using var conn = Mocker.Resolve<IMainDatabase>().OpenConnection();
            conn.Query<int>("SELECT \"Quality\" FROM \"Music_QualityDefinitions\"").Should().BeEquivalentTo(AudioQuality.All.Select(q => q.Id));
        }

        [Test]
        public void should_round_trip_titles_and_sizes()
        {
            var definition = new AudioQualityDefinition(AudioQuality.FLAC) { Title = "Lossless", MinSize = 1.5, MaxSize = 99.5, PreferredSize = 50 };

            Subject.Insert(definition);

            var stored = Subject.Get(definition.Id);
            stored.Quality.Should().Be(AudioQuality.FLAC);
            stored.Title.Should().Be("Lossless");
            stored.MinSize.Should().Be(1.5);
            stored.MaxSize.Should().Be(99.5);
            stored.PreferredSize.Should().Be(50);
        }

        [Test]
        public void should_keep_one_definition_per_quality()
        {
            Subject.Insert(new AudioQualityDefinition(AudioQuality.FLAC));

            System.Action insert = () => Subject.Insert(new AudioQualityDefinition(AudioQuality.FLAC) { Title = "Other" });

            insert.Should().Throw<System.Exception>();
        }
    }
}
