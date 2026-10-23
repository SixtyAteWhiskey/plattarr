using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.AutoTagging;
using NzbDrone.Core.Tv.AutoTagging.Specifications;
using MovieAutoTag = NzbDrone.Core.Movies.AutoTagging.AutoTag;
using MovieAutoTaggingRepository = NzbDrone.Core.Movies.AutoTagging.AutoTaggingRepository;
using MovieGenreSpecification = NzbDrone.Core.Movies.AutoTagging.Specifications.GenreSpecification;
using MovieIAutoTaggingSpecification = NzbDrone.Core.Movies.AutoTagging.Specifications.IAutoTaggingSpecification;

namespace NzbDrone.Core.Test.TvTests.AutoTagging
{
    [TestFixture]
    public class TvAutoTaggingRepositoryFixture : DbTest<AutoTaggingRepository, AutoTag>
    {
        private static AutoTag NewAutoTag(string name = "Anime on AMC")
        {
            return new AutoTag
            {
                Name = name,
                RemoveTagsAutomatically = true,
                Tags = new HashSet<int> { 2, 5 },
                Specifications = new List<IAutoTaggingSpecification>
                {
                    new NetworkSpecification { Name = "Network", Value = new[] { "AMC", "HBO" }, Required = true },
                    new SeriesTypeSpecification { Name = "Type", Value = (int)SeriesTypes.Anime, Negate = true },
                    new YearSpecification { Name = "Years", Min = 2000, Max = 2010 },
                    new TagSpecification { Name = "Tag", Value = 9 },
                    new MonitoredSpecification { Name = "Monitored" }
                }
            };
        }

        [Test]
        public void should_round_trip_every_kind_of_specification()
        {
            var autoTag = NewAutoTag();

            Subject.Insert(autoTag);
            var stored = Subject.Get(autoTag.Id);

            stored.Name.Should().Be("Anime on AMC");
            stored.RemoveTagsAutomatically.Should().BeTrue();
            stored.Tags.Should().BeEquivalentTo(new[] { 2, 5 });
            stored.Specifications.Select(s => s.GetType()).Should().Equal(
                typeof(NetworkSpecification),
                typeof(SeriesTypeSpecification),
                typeof(YearSpecification),
                typeof(TagSpecification),
                typeof(MonitoredSpecification));

            var network = stored.Specifications[0].Should().BeOfType<NetworkSpecification>().Subject;
            network.Value.Should().Equal("AMC", "HBO");
            network.Name.Should().Be("Network");
            network.Required.Should().BeTrue();
            network.Negate.Should().BeFalse();

            var type = stored.Specifications[1].Should().BeOfType<SeriesTypeSpecification>().Subject;
            type.Value.Should().Be((int)SeriesTypes.Anime);
            type.Negate.Should().BeTrue();

            var years = stored.Specifications[2].Should().BeOfType<YearSpecification>().Subject;
            years.Min.Should().Be(2000);
            years.Max.Should().Be(2010);

            stored.Specifications[3].Should().BeOfType<TagSpecification>().Which.Value.Should().Be(9);
        }

        [Test]
        public void should_refuse_a_second_auto_tag_with_the_same_name()
        {
            Subject.Insert(NewAutoTag());

            var action = () => Subject.Insert(NewAutoTag());

            action.Should().Throw<System.Exception>();
        }

        [Test]
        public void should_keep_the_movie_auto_tags_in_their_own_table()
        {
            var movieRepository = Mocker.Resolve<MovieAutoTaggingRepository>();

            movieRepository.Insert(new MovieAutoTag
            {
                Name = "Movie comedy",
                Tags = new HashSet<int> { 1 },
                Specifications = new List<MovieIAutoTaggingSpecification>
                {
                    new MovieGenreSpecification { Name = "Genre", Value = new[] { "Comedy" } }
                }
            });
            Subject.Insert(NewAutoTag());

            Subject.All().Should().ContainSingle().Which.Name.Should().Be("Anime on AMC");
            movieRepository.All().Should().ContainSingle().Which.Name.Should().Be("Movie comedy");
        }
    }
}
