using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class ArtistMetadataRepositoryFixture : DbTest<ArtistMetadataRepository, ArtistMetadata>
    {
        private List<ArtistMetadata> _metadataList;

        [SetUp]
        public void Setup()
        {
            _metadataList = Builder<ArtistMetadata>.CreateListOfSize(10).All().With(x => x.Id = 0).BuildList();
        }

        [Test]
        public void upsert_many_should_insert_list_of_new()
        {
            var updated = Subject.UpsertMany(_metadataList);
            AllStoredModels.Should().HaveCount(_metadataList.Count);
            updated.Should().BeTrue();
        }

        [Test]
        public void upsert_many_should_upsert_existing_with_id_0()
        {
            var clone = _metadataList.JsonClone();
            var updated = Subject.UpsertMany(clone);

            updated.Should().BeTrue();
            AllStoredModels.Should().HaveCount(_metadataList.Count);

            updated = Subject.UpsertMany(_metadataList);
            updated.Should().BeFalse();
            AllStoredModels.Should().HaveCount(_metadataList.Count);
        }

        [Test]
        public void upsert_many_should_upsert_mixed_list_of_old_and_new()
        {
            var clone = _metadataList.Take(5).ToList().JsonClone();
            var updated = Subject.UpsertMany(clone);

            updated.Should().BeTrue();
            AllStoredModels.Should().HaveCount(clone.Count);

            updated = Subject.UpsertMany(_metadataList);
            updated.Should().BeTrue();
            AllStoredModels.Should().HaveCount(_metadataList.Count);
        }

        [Test]
        public void upsert_many_should_replace_the_images_of_an_existing_row()
        {
            var first = MusicTestData.NewMetadata();
            first.Images = new List<MediaCover.MediaCover> { new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/old.jpg") };
            Subject.UpsertMany(new List<ArtistMetadata> { first });

            var second = MusicTestData.NewMetadata();
            second.Images = new List<MediaCover.MediaCover>
            {
                new MediaCover.MediaCover(MediaCoverTypes.Poster, "https://img.example/new-poster.jpg"),
                new MediaCover.MediaCover(MediaCoverTypes.Fanart, "https://img.example/new-fanart.jpg")
            };

            Subject.UpsertMany(new List<ArtistMetadata> { second }).Should().BeTrue();

            AllStoredModels.Should().ContainSingle().Which.Images.Select(i => i.RemoteUrl)
                .Should().Equal("https://img.example/new-poster.jpg", "https://img.example/new-fanart.jpg");
        }

        [Test]
        public void should_round_trip_every_column()
        {
            var metadata = MusicTestData.NewMetadata();

            Subject.Insert(metadata);

            StoredModel.Should().BeEquivalentTo(metadata);
        }
    }
}
