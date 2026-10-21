using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class MediaKindScopeFixture : CoreTest<CustomFormatCalculationService>
    {
        private static CustomFormat Format(string name, params MediaKind[] appliesTo)
        {
            return new CustomFormat(name, new ReleaseTitleSpecification { Name = "Any title", Value = ".*" })
            {
                Id = name.GetHashCode(),
                AppliesTo = appliesTo.ToList()
            };
        }

        [Test]
        public void movie_releases_should_only_score_formats_that_apply_to_movies()
        {
            Mocker.GetMock<ICustomFormatService>()
                  .Setup(s => s.All())
                  .Returns(new List<CustomFormat>
                  {
                      Format("Everything"),
                      Format("Movies only", MediaKind.Movies),
                      Format("Movies and TV", MediaKind.Movies, MediaKind.Tv),
                      Format("TV only", MediaKind.Tv)
                  });

            var localMovie = new LocalMovie
            {
                Movie = Builder<Movie>.CreateNew().Build(),
                Path = "/movies/Interstellar (2014)/Interstellar.2014.1080p.BluRay.x264-GRP.mkv",
                SceneName = "Interstellar.2014.1080p.BluRay.x264-GRP",
                Quality = new QualityModel(Quality.Bluray1080p),
                Languages = new List<Language> { Language.English }
            };

            Subject.ParseCustomFormat(localMovie).Select(f => f.Name)
                   .Should().BeEquivalentTo("Everything", "Movies only", "Movies and TV");
        }

        [Test]
        public void empty_or_null_applies_to_should_mean_every_media_type()
        {
            var format = new CustomFormat { AppliesTo = null };

            format.AppliesToKind(MediaKind.Music).Should().BeTrue();

            format.AppliesTo = new List<MediaKind>();
            format.AppliesToKind(MediaKind.Tv).Should().BeTrue();
        }
    }
}
