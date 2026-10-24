using System;
using System.Collections.Generic;
using System.Data.SQLite;
using FluentAssertions;
using Npgsql;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.Parser;
using NzbDrone.Core.Music.Profiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class ArtistRepositoryFixture : DbTest<ArtistRepository, Artist>
    {
        private ArtistMetadataRepository _artistMetadataRepo;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<ICustomFormatService>().Setup(s => s.All()).Returns(new List<CustomFormat>());
            _artistMetadataRepo = Mocker.Resolve<ArtistMetadataRepository>();
        }

        private Artist AddArtist(string name, string foreignId, List<string> oldIds = null)
        {
            var metadata = MusicTestData.NewMetadata(name, foreignId);
            metadata.OldForeignArtistIds = oldIds ?? new List<string>();
            _artistMetadataRepo.Insert(metadata);

            var artist = MusicTestData.NewArtist(metadata.Id, name: name);
            artist.Path = "/data/music/" + foreignId;
            Subject.Insert(artist);

            return artist;
        }

        private void GivenArtists()
        {
            AddArtist("The Black Eyed Peas", "d5be5333-4171-427e-8e12-732087c6b78e");
            AddArtist("The Black Keys", "d15721d8-56b4-453d-b506-fc915b14cba2", new List<string> { "6f2ed437-825c-4cea-bb58-bf7688c6317a" });
        }

        [Test]
        public void should_round_trip_every_column_and_the_metadata()
        {
            var metadata = MusicTestData.NewMetadata();
            _artistMetadataRepo.Insert(metadata);
            var artist = MusicTestData.NewArtist(metadata.Id);

            Subject.Insert(artist);

            var stored = Subject.Get(artist.Id);

            stored.Should().BeEquivalentTo(artist, o => o.WithDateTolerance().Excluding(a => a.Metadata).Excluding(a => a.QualityProfile).Excluding(a => a.MetadataProfile).Excluding(a => a.Albums));
            stored.Metadata.Value.Should().BeEquivalentTo(metadata);
            stored.Tags.Should().BeEquivalentTo(new[] { 3, 5 });
            stored.AddOptions.Monitor.Should().Be(MonitorTypes.Future);
        }

        [Test]
        public void should_lazyload_profiles()
        {
            var profile = MusicTestData.NewQualityProfile();
            var metaProfile = MusicTestData.NewMetadataProfile();

            Mocker.Resolve<AudioQualityProfileRepository>().Insert(profile);
            Mocker.Resolve<MetadataProfileRepository>().Insert(metaProfile);

            var metadata = MusicTestData.NewMetadata();
            _artistMetadataRepo.Insert(metadata);
            var artist = MusicTestData.NewArtist(metadata.Id, profile.Id, metaProfile.Id);

            Subject.Insert(artist);

            StoredModel.QualityProfile.Value.Name.Should().Be(profile.Name);
            StoredModel.MetadataProfile.Value.Name.Should().Be(metaProfile.Name);
        }

        [TestCase("The Black Eyed Peas")]
        [TestCase("The Black Keys")]
        public void should_find_artist_in_db_by_name(string name)
        {
            GivenArtists();
            var artist = Subject.FindByName(MusicTestData.Clean(name));

            artist.Should().NotBeNull();
            artist.Name.Should().Be(name);
        }

        // ArtistService.FindByName looks an artist up by title.CleanArtistName(), and the clean name is
        // stored when the artist is added, so the parser and the repository have to agree.
        [TestCase("Beyoncé", "BEYONCE")]
        [TestCase("Motörhead", "motorhead")]
        [TestCase("Earth, Wind & Fire", "Earth Wind and Fire")]
        [TestCase("Florence + The Machine", "florence  the machine")]
        public void should_find_artist_by_clean_name_regardless_of_accents_case_and_punctuation(string stored, string searched)
        {
            GivenArtists();
            AddArtist(stored, "0f6c2c6e-5d31-4a57-a1f6-0c1c7c2fa001");

            var artist = Subject.FindByName(searched.CleanArtistName());

            artist.Should().NotBeNull();
            artist.Name.Should().Be(stored);
        }

        [Test]
        public void should_find_artist_in_by_id()
        {
            GivenArtists();
            var artist = Subject.FindById("d5be5333-4171-427e-8e12-732087c6b78e");

            artist.Should().NotBeNull();
            artist.ForeignArtistId.Should().Be("d5be5333-4171-427e-8e12-732087c6b78e");
        }

        [Test]
        public void should_find_artist_in_by_old_id()
        {
            GivenArtists();
            var artist = Subject.FindById("6f2ed437-825c-4cea-bb58-bf7688c6317a");

            artist.Should().NotBeNull();
            artist.Name.Should().Be("The Black Keys");
            artist.ForeignArtistId.Should().Be("d15721d8-56b4-453d-b506-fc915b14cba2");
        }

        [Test]
        public void should_not_find_artist_if_multiple_artists_have_same_name()
        {
            GivenArtists();

            var name = "Alice Cooper";
            AddArtist(name, "ee58c59f-8e7f-4430-b8ca-236c4d3745ae");
            AddArtist(name, "4d7928cd-7ed2-4282-8c29-c0c9f966f1bd");

            Subject.All().Should().HaveCount(4);

            Action act = () => Subject.FindByName(MusicTestData.Clean(name));
            act.Should().Throw<MultipleArtistsFoundException>();
        }

        [Test]
        public void should_throw_sql_exception_adding_duplicate_artist()
        {
            var metadata = MusicTestData.NewMetadata();
            _artistMetadataRepo.Insert(metadata);

            Subject.Insert(MusicTestData.NewArtist(metadata.Id));

            Action insertDupe = () => Subject.Insert(MusicTestData.NewArtist(metadata.Id));

            if (Db.DatabaseType == DatabaseType.PostgreSQL)
            {
                insertDupe.Should().Throw<PostgresException>();
            }
            else
            {
                insertDupe.Should().Throw<SQLiteException>();
            }
        }

        [Test]
        public void should_report_paths_and_tags_by_artist_id()
        {
            var artist = AddArtist("The Black Keys", "d15721d8-56b4-453d-b506-fc915b14cba2");

            Subject.AllArtistPaths().Should().Contain(artist.Id, artist.Path);
            Subject.AllArtistsTags()[artist.Id].Should().BeEquivalentTo(new[] { 3, 5 });
            Subject.ArtistPathExists(artist.Path).Should().BeTrue();
            Subject.ArtistPathExists("/nowhere").Should().BeFalse();
        }
    }
}
