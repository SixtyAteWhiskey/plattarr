using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.CustomFormats
{
    [TestFixture]
    public class CustomFormatRepositoryFixture : DbTest<CustomFormatRepository, CustomFormat>
    {
        [Test]
        public void should_round_trip_applies_to()
        {
            var format = new CustomFormat("x265", new ReleaseTitleSpecification { Name = "x265", Value = "x265" })
            {
                AppliesTo = new List<MediaKind> { MediaKind.Movies, MediaKind.Tv }
            };

            Subject.Insert(format);

            StoredModel.AppliesTo.Should().Equal(MediaKind.Movies, MediaKind.Tv);
        }

        [Test]
        public void applies_to_should_default_to_empty()
        {
            Subject.Insert(new CustomFormat("x265", new ReleaseTitleSpecification { Name = "x265", Value = "x265" }));

            StoredModel.AppliesTo.Should().BeEmpty();
        }
    }
}
