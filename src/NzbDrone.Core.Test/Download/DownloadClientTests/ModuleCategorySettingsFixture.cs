using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentValidation;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.Clients;
using NzbDrone.Core.Download.Clients.QBittorrent;
using NzbDrone.Core.Download.Clients.Sabnzbd;
using NzbDrone.Core.Modules;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.DownloadClientTests
{
    [TestFixture]
    public class ModuleCategorySettingsFixture : TestBase
    {
        private QBittorrentSettings _settings;

        [SetUp]
        public void Setup()
        {
            _settings = new QBittorrentSettings { Host = "localhost", Port = 8080 };
            _settings.Movies().Category = "movies";
        }

        [Test]
        public void a_new_client_has_the_movies_entry_with_the_default_category()
        {
            var settings = new QBittorrentSettings();

            settings.ModuleCategories.Should().ContainSingle();
            settings.ModuleCategories[0].Kind.Should().Be(MediaKind.Movies);
            settings.ModuleCategories[0].Category.Should().Be("plattarr-movies");
        }

        [Test]
        public void a_new_client_starts_with_the_priority_its_class_declares()
        {
            new QBittorrentSettings().Movies().RecentPriority.Should().Be((int)QBittorrentPriority.Last);
            new SabnzbdSettings().Movies().OlderPriority.Should().Be((int)SabnzbdPriority.Default);
        }

        [TestCase(MediaKind.Movies, "plattarr-movies")]
        [TestCase(MediaKind.Tv, "plattarr-tv")]
        [TestCase(MediaKind.Music, "plattarr-music")]
        public void every_media_kind_has_a_default_category(MediaKind kind, string expected)
        {
            ModuleCategorySettings.DefaultCategory(kind).Should().Be(expected);
        }

        [Test]
        public void For_should_return_the_entry_of_the_kind()
        {
            _settings.AddTv("tv");

            _settings.For(MediaKind.Tv).Category.Should().Be("tv");
            _settings.For(MediaKind.Movies).Category.Should().Be("movies");
        }

        [Test]
        public void For_should_throw_when_the_kind_has_no_entry()
        {
            var act = () => _settings.For(MediaKind.Music);

            act.Should().Throw<DownloadClientException>().WithMessage("*no category settings for Music*");
        }

        [Test]
        public void ForCategory_should_match_case_insensitively()
        {
            _settings.AddTv("TV-Shows");

            _settings.ForCategory("tv-shows").Kind.Should().Be(MediaKind.Tv);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("something-else")]
        public void ForCategory_should_fall_back_to_movies_for_a_category_no_entry_claims(string category)
        {
            _settings.AddTv("tv");

            _settings.ForCategory(category).Kind.Should().Be(MediaKind.Movies);
        }

        [Test]
        public void ForCategory_should_not_match_an_entry_with_an_empty_category()
        {
            _settings.AddTv(null);

            _settings.ForCategory("").Kind.Should().Be(MediaKind.Movies);
        }

        [Test]
        public void ForItem_should_use_the_category_of_the_download()
        {
            _settings.AddTv("tv");

            _settings.ForItem(new DownloadClientItem { Category = "tv" }).Kind.Should().Be(MediaKind.Tv);
        }

        [Test]
        public void MonitoredCategories_should_list_each_non_empty_category_once()
        {
            _settings.AddTv("TV");
            _settings.AddTv("tv");
            _settings.AddTv("");

            _settings.MonitoredCategories().Should().Equal("movies", "TV");
        }

        [Test]
        public void Monitors_should_take_everything_when_no_category_is_configured()
        {
            _settings.Movies().Category = "";

            _settings.Monitors("anything").Should().BeTrue();
            _settings.Monitors(null).Should().BeTrue();
        }

        [Test]
        public void Monitors_should_take_only_the_configured_categories()
        {
            _settings.AddTv("tv");

            _settings.Monitors("movies").Should().BeTrue();
            _settings.Monitors("TV").Should().BeTrue();
            _settings.Monitors("other").Should().BeFalse();
            _settings.Monitors(null).Should().BeFalse();
        }

        [Test]
        public void entries_should_be_equal_when_their_values_are()
        {
            var a = new ModuleCategorySettings { Kind = MediaKind.Tv, Category = "tv", ImportedCategory = "done", RecentPriority = 1, OlderPriority = 2 };
            var b = new ModuleCategorySettings { Kind = MediaKind.Tv, Category = "tv", ImportedCategory = "done", RecentPriority = 1, OlderPriority = 2 };

            a.Equals(b).Should().BeTrue();
            (a == b).Should().BeTrue();

            b.OlderPriority = 3;

            a.Equals(b).Should().BeFalse();
        }

        [Test]
        public void settings_should_be_equal_only_when_their_entries_are()
        {
            var a = new QBittorrentSettings();
            var b = new QBittorrentSettings();

            a.Equals(b).Should().BeTrue();

            b.AddTv("tv");

            a.Equals(b).Should().BeFalse();

            a.AddTv("tv");

            a.Equals(b).Should().BeTrue();

            a.Entry(MediaKind.Tv).Category = "other";

            a.Equals(b).Should().BeFalse();
        }
    }

    [TestFixture]
    public class ModuleCategoryValidationFixture : TestBase
    {
        private class Holder
        {
            public List<ModuleCategorySettings> ModuleCategories { get; set; } = new List<ModuleCategorySettings>();
        }

        private class HolderValidator : AbstractValidator<Holder>
        {
            public HolderValidator()
            {
                RuleFor(c => c.ModuleCategories).UniqueCategories();
            }
        }

        private static ModuleCategorySettings Entry(MediaKind kind, string category)
        {
            return new ModuleCategorySettings { Kind = kind, Category = category };
        }

        private static FluentValidation.Results.ValidationResult Validate(params ModuleCategorySettings[] entries)
        {
            return new HolderValidator().Validate(new Holder { ModuleCategories = new List<ModuleCategorySettings>(entries) });
        }

        [Test]
        public void should_accept_distinct_categories()
        {
            Validate(Entry(MediaKind.Movies, "movies"), Entry(MediaKind.Tv, "tv")).IsValid.Should().BeTrue();
        }

        [Test]
        public void should_refuse_a_category_two_media_types_share_and_name_the_field_of_the_second()
        {
            var result = Validate(Entry(MediaKind.Movies, "shared"), Entry(MediaKind.Tv, "Shared"));

            result.IsValid.Should().BeFalse();
            result.Errors.Should().ContainSingle();
            result.Errors[0].PropertyName.Should().Be("TvCategory");
            result.Errors[0].ErrorMessage.Should().Contain("Movies");
        }

        [Test]
        public void should_let_any_number_of_entries_have_no_category()
        {
            Validate(Entry(MediaKind.Movies, ""), Entry(MediaKind.Tv, null), Entry(MediaKind.Music, " ")).IsValid.Should().BeTrue();
        }

        [Test]
        public void a_client_validator_should_name_the_field_of_the_entry_that_is_wrong()
        {
            var settings = new QBittorrentSettings { Host = "localhost" };
            settings.Movies().Category = "ok-category";
            settings.AddTv("/not/valid/");

            var result = settings.Validate();

            result.IsValid.Should().BeFalse();
            result.Errors.Select(e => e.PropertyName).Should().Equal("TvCategory");
        }

        [Test]
        public void a_client_validator_should_accept_every_entry_when_all_are_right()
        {
            var settings = new QBittorrentSettings { Host = "localhost" };
            settings.Movies().Category = "ok-category";
            settings.AddTv("tv-category");

            settings.Validate().IsValid.Should().BeTrue();
        }

        [Test]
        public void a_client_that_recommends_a_category_should_warn_once_per_entry_without_one()
        {
            var settings = new SabnzbdSettings { Host = "localhost", ApiKey = "key" };
            settings.Movies().Category = "movies";
            settings.AddTv("");

            var result = settings.Validate();

            result.IsValid.Should().BeTrue();
            result.Warnings.Select(w => w.PropertyName).Should().Equal("TvCategory");
        }

        [Test]
        public void a_client_should_refuse_the_same_category_for_two_media_types()
        {
            var settings = new QBittorrentSettings { Host = "localhost" };
            settings.Movies().Category = "shared";
            settings.AddTv("shared");

            var result = settings.Validate();

            result.IsValid.Should().BeFalse();
            result.Errors.Select(e => e.PropertyName).Should().Contain("TvCategory");
        }
    }
}
