using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Music;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests
{
    [TestFixture]
    public class AlbumYearMatcherFixture : CoreTest<AlbumYearMatcher>
    {
        private static DateTime Date(int year) => new DateTime(year, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void should_match_without_adjustment_when_the_release_has_no_year()
        {
            var result = Subject.Match(Date(2000), null);

            result.IsMatch.Should().BeTrue();
            result.YearDifference.Should().BeNull();
            result.ScoreAdjustment.Should().Be(0);
            result.Confidence.Should().Be(YearMatchConfidence.None);
        }

        [Test]
        public void should_match_with_low_confidence_when_the_album_has_no_date()
        {
            var result = Subject.Match((DateTime?)null, 2000);

            result.IsMatch.Should().BeTrue();
            result.ScoreAdjustment.Should().Be(0);
            result.Confidence.Should().Be(YearMatchConfidence.Low);
        }

        [Test]
        public void should_give_the_exact_bonus_for_the_same_year()
        {
            var result = Subject.Match(Date(2000), 2000);

            result.IsMatch.Should().BeTrue();
            result.YearDifference.Should().Be(0);
            result.ScoreAdjustment.Should().Be(0.20);
            result.Confidence.Should().Be(YearMatchConfidence.High);
        }

        [TestCase(1999)]
        [TestCase(2001)]
        public void should_give_the_close_bonus_for_one_year_apart(int parsed)
        {
            var result = Subject.Match(Date(2000), parsed);

            result.IsMatch.Should().BeTrue();
            result.YearDifference.Should().Be(1);
            result.ScoreAdjustment.Should().Be(0.10);
            result.Confidence.Should().Be(YearMatchConfidence.High);
        }

        [TestCase(2002, 0.05, YearMatchConfidence.Medium)]
        [TestCase(2003, 0.10, YearMatchConfidence.Medium)]
        [TestCase(2004, 0.15, YearMatchConfidence.Low)]
        [TestCase(2005, 0.20, YearMatchConfidence.Low)]
        public void should_penalize_each_year_beyond_the_tolerance(int parsed, double penalty, YearMatchConfidence confidence)
        {
            var result = Subject.Match(Date(2000), parsed);

            result.IsMatch.Should().BeTrue();
            result.ScoreAdjustment.Should().BeApproximately(-penalty, 0.0001);
            result.Confidence.Should().Be(confidence);
        }

        [TestCase(2006)]
        [TestCase(1990)]
        public void should_reject_more_than_five_years_apart(int parsed)
        {
            var result = Subject.Match(Date(2000), parsed);

            result.IsMatch.Should().BeFalse();
            result.RejectionReason.Should().Contain(parsed.ToString());
            result.ScoreAdjustment.Should().BeApproximately(-0.25, 0.0001);
            result.Confidence.Should().Be(YearMatchConfidence.High);
        }

        [Test]
        public void should_match_a_year_from_a_monitored_release_such_as_a_remaster()
        {
            var album = new Album
            {
                ReleaseDate = Date(1975),
                AlbumReleases = new LazyLoaded<List<AlbumRelease>>(new List<AlbumRelease>
                {
                    new AlbumRelease { ReleaseDate = Date(2011), Monitored = true }
                })
            };

            var result = Subject.Match(album, 2011);

            result.IsMatch.Should().BeTrue();
            result.YearDifference.Should().Be(0);
            result.Confidence.Should().Be(YearMatchConfidence.High);
        }

        [Test]
        public void should_ignore_an_unmonitored_release_unless_any_release_is_ok()
        {
            var releases = new List<AlbumRelease> { new AlbumRelease { ReleaseDate = Date(2011), Monitored = false } };

            var album = new Album { ReleaseDate = Date(1975), AlbumReleases = new LazyLoaded<List<AlbumRelease>>(releases) };
            Subject.Match(album, 2011).IsMatch.Should().BeFalse();

            album.AnyReleaseOk = true;
            Subject.Match(album, 2011).IsMatch.Should().BeTrue();
        }

        [TestCase("Compilation")]
        [TestCase("Live")]
        public void should_be_lenient_for_compilations_and_live_albums(string type)
        {
            var album = new Album
            {
                ReleaseDate = Date(2000),
                SecondaryTypes = new List<SecondaryAlbumType> { type == "Live" ? SecondaryAlbumType.Live : SecondaryAlbumType.Compilation }
            };

            var result = Subject.Match(album, 2005);

            // five years apart costs a studio album 0.20 (below); these types pay one year's penalty
            result.IsMatch.Should().BeTrue();
            result.Confidence.Should().Be(YearMatchConfidence.Low);
            result.ScoreAdjustment.Should().BeApproximately(-0.05, 0.0001);
        }

        [Test]
        public void should_still_reject_a_live_album_more_than_five_years_apart()
        {
            var album = new Album { ReleaseDate = Date(2000), SecondaryTypes = new List<SecondaryAlbumType> { SecondaryAlbumType.Live } };

            Subject.Match(album, 2006).IsMatch.Should().BeFalse();
        }

        [Test]
        public void should_not_be_lenient_for_a_studio_album()
        {
            var album = new Album { ReleaseDate = Date(2000), SecondaryTypes = new List<SecondaryAlbumType> { SecondaryAlbumType.Studio } };

            Subject.Match(album, 2005).Confidence.Should().Be(YearMatchConfidence.Low);
            Subject.Match(album, 2005).ScoreAdjustment.Should().BeApproximately(-0.20, 0.0001);
        }

        [Test]
        public void should_return_no_year_for_an_album_when_none_was_parsed()
        {
            Subject.Match(new Album { ReleaseDate = Date(2000) }, null).Confidence.Should().Be(YearMatchConfidence.None);
        }

        [Test]
        public void year_score_should_be_the_match_adjustment()
        {
            Subject.CalculateYearScore(Date(2000), 2000).Should().Be(0.20);
            Subject.CalculateYearScore(Date(2000), null).Should().Be(0);
        }
    }
}
