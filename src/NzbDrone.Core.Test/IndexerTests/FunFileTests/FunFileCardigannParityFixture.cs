using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Datastore.Converters;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Definitions;
using NzbDrone.Core.Indexers.Definitions.Cardigann;
using NzbDrone.Core.Indexers.Settings;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.ThingiProvider;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NzbDrone.Core.Test.IndexerTests.FunFileTests
{
    [TestFixture]
    public class FunFileCardigannParityFixture : CoreTest
    {
        // Prowlarr/Indexers 5f5db4e58d06be18619c3a858b9a91ade6f341ca, definitions/v11/funfile.yml.
        private const string DefinitionPath = "Files/Indexers/FunFile/funfile-5f5db4e.yml";
        private const string BaseUrl = "https://www.funfile.org/";
        private const string ResultHtml = """
            <table class="mainframe"><tr><td><table cellpadding="2"><tbody>
            <tr>
              <td class="row3"><a href="browse.php?cat=19">Movies</a></td>
              <td class="row3"><a href="details.php?id=42&amp;hit=1" title="Example Film">Example Film</a></td>
              <td class="row3"><a href="download.php?id=42">Download</a></td>
              <td class="row3">3</td>
              <td class="row3">-</td>
              <td class="row3">2 hours ago</td>
              <td class="row3">-</td>
              <td class="row3">1.5 GB</td>
              <td class="row3">7</td>
              <td class="row3">11</td>
              <td class="row3">2</td>
            </tr>
            <tr id="ka42"><td class="row2" colspan="11">
              <table><tbody><tr><td>-</td></tr><tr><td>-</td></tr><tr><td>2X</td><td>0X</td></tr></tbody></table>
              <span style="float:left">action, thriller</span>
            </td></tr>
            </tbody></table></td></tr></table>
            """;

        private CardigannDefinition _definition;
        private CardigannSettings _settings;
        private CardigannRequestGenerator _cardigann;
        private FunFileRequestGenerator _native;

        [SetUp]
        public void SetUp()
        {
            _definition = new DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build()
                .Deserialize<CardigannDefinition>(ReadAllText(DefinitionPath));

            const string nativeJson = """
                {"baseUrl":"https://www.funfile.org/","username":"sample-user","password":"FAKE-PASSWORD"}
                """;
            var converter = new ProviderSettingConverter();
            _settings = (CardigannSettings)converter.Parse(FunFileSettingsConverter.Convert(nativeJson), typeof(CardigannSettings));

            _cardigann = new CardigannRequestGenerator(Mocker.Resolve<IConfigService>(), _definition, TestLogger, TimeSpan.Zero)
            {
                Settings = _settings,
                Definition = new IndexerDefinition { Name = "FunFile", Implementation = "Cardigann", Settings = _settings }
            };

            var capabilities = new IndexerCapabilities();
            capabilities.Categories.AddCategoryMapping(44, NewznabStandardCategory.TVAnime, "Anime");
            capabilities.Categories.AddCategoryMapping(22, NewznabStandardCategory.PC, "Applications");
            capabilities.Categories.AddCategoryMapping(43, NewznabStandardCategory.AudioAudiobook, "Audio Books");
            capabilities.Categories.AddCategoryMapping(4, NewznabStandardCategory.PCGames, "Games");
            capabilities.Categories.AddCategoryMapping(40, NewznabStandardCategory.OtherMisc, "Miscellaneous");
            capabilities.Categories.AddCategoryMapping(19, NewznabStandardCategory.Movies, "Movies");
            capabilities.Categories.AddCategoryMapping(7, NewznabStandardCategory.TV, "TV");
            capabilities.Categories.AddCategoryMapping(6, NewznabStandardCategory.Audio, "Music");
            capabilities.Categories.AddCategoryMapping(31, NewznabStandardCategory.PCMobileOther, "Portable");
            capabilities.Categories.AddCategoryMapping(49, NewznabStandardCategory.Other, "Tutorials");
            capabilities.Categories.AddCategoryMapping(27, NewznabStandardCategory.Books, "Ebook");
            _native = new FunFileRequestGenerator(
                (UserPassTorrentBaseSettings)converter.Parse(nativeJson, typeof(UserPassTorrentBaseSettings)), capabilities);
        }

        private static Dictionary<string, string> Query(IndexerRequest request)
        {
            return QueryHelpers.ParseQuery(request.Url.Query).ToDictionary(p => p.Key, p => p.Value.ToString());
        }

        private static IndexerResponse Response(IndexerRequest request)
        {
            return new IndexerResponse(request, new HttpResponse(request.HttpRequest, new HttpHeader(), ResultHtml));
        }

        [Test]
        public void should_generate_the_same_movie_term_request()
        {
            var criteria = new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };

            var native = _native.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var cardigann = _cardigann.GetSearchRequests(criteria).GetAllTiers().Single().Single();

            cardigann.Url.Path.Should().Be(native.Url.Path);
            Query(cardigann).Should().BeEquivalentTo(Query(native));
        }

        [Test]
        public void should_expose_the_imdb_title_search_difference()
        {
            var criteria = new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                ImdbId = "0076759",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };

            var native = Query(_native.GetSearchRequests(criteria).GetAllTiers().Single().Single());
            var cardigann = Query(_cardigann.GetSearchRequests(criteria).GetAllTiers().Single().Single());

            cardigann["search"].Should().Be(native["search"]);
            cardigann["c19"].Should().Be(native["c19"]);
            cardigann["s_desc"].Should().Be(native["s_desc"]);
            native["s_title"].Should().Be("1");
            cardigann.Should().NotContainKey("s_title");
        }

        [Test]
        public void candidate_definition_should_match_native_imdb_request()
        {
            _definition.Search.Inputs["s_title"] = "1";
            var criteria = new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                ImdbId = "0076759",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };

            var native = _native.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var cardigann = _cardigann.GetSearchRequests(criteria).GetAllTiers().Single().Single();

            cardigann.Url.Path.Should().Be(native.Url.Path);
            Query(cardigann).Should().BeEquivalentTo(Query(native));
        }

        [Test]
        public void candidate_definition_should_match_other_supported_search_modes()
        {
            _definition.Search.Inputs["s_title"] = "1";

            void AssertSame(IndexerRequest native, IndexerRequest cardigann, string mode)
            {
                cardigann.Url.Path.Should().Be(native.Url.Path, mode);
                Query(cardigann).Should().BeEquivalentTo(Query(native), mode);
            }

            var tv = new TvSearchCriteria
            {
                SearchTerm = "Example Show",
                Season = 2,
                Episode = "03",
                Categories = new[] { NewznabStandardCategory.TV.Id }
            };
            AssertSame(
                _native.GetSearchRequests(tv).GetAllTiers().Single().Single(),
                _cardigann.GetSearchRequests(tv).GetAllTiers().Single().Single(),
                "TV term and episode");

            tv.ImdbId = "0076759";
            AssertSame(
                _native.GetSearchRequests(tv).GetAllTiers().Single().Single(),
                _cardigann.GetSearchRequests(tv).GetAllTiers().Single().Single(),
                "TV IMDb");

            var music = new MusicSearchCriteria
            {
                SearchTerm = "Example Album",
                Categories = new[] { NewznabStandardCategory.Audio.Id }
            };
            AssertSame(
                _native.GetSearchRequests(music).GetAllTiers().Single().Single(),
                _cardigann.GetSearchRequests(music).GetAllTiers().Single().Single(),
                "music term");

            var book = new BookSearchCriteria
            {
                SearchTerm = "Example Book",
                Categories = new[] { NewznabStandardCategory.Books.Id }
            };
            AssertSame(
                _native.GetSearchRequests(book).GetAllTiers().Single().Single(),
                _cardigann.GetSearchRequests(book).GetAllTiers().Single().Single(),
                "book term");

            var basic = new BasicSearchCriteria
            {
                SearchTerm = "Example Release",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };
            AssertSame(
                _native.GetSearchRequests(basic).GetAllTiers().Single().Single(),
                _cardigann.GetSearchRequests(basic).GetAllTiers().Single().Single(),
                "basic term");
        }

        [Test]
        public async Task should_post_the_form_action_after_loading_the_login_page()
        {
            var requests = new List<HttpRequest>();
            Mocker.GetMock<IIndexerHttpClient>()
                .Setup(c => c.ExecuteProxiedAsync(It.IsAny<HttpRequest>(), It.IsAny<ProviderDefinition>()))
                .Returns((HttpRequest request, ProviderDefinition _) =>
                {
                    requests.Add(request);
                    var html = request.Method == HttpMethod.Get
                        ? "<form name=\"login\" action=\"takelogin.php\"><input name=\"username\"><input name=\"password\"></form>"
                        : "<a href=\"logout.php\">Log out</a>";
                    return Task.FromResult(new HttpResponse(request, new HttpHeader(), html));
                });

            _cardigann.HttpClient = Mocker.Resolve<IIndexerHttpClient>();
            _cardigann.CookiesUpdater = (_, _) => { };

            await _cardigann.DoLogin();

            requests.Select(r => r.Url.Path).Should().Equal("/login.php", "/takelogin.php");
            requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Post);
            requests[1].GetContent().Should().Contain("username=sample-user");
            requests[1].GetContent().Should().Contain("password=FAKE-PASSWORD");
        }

        [Test]
        public void should_compare_parsed_result_fields_on_the_same_html()
        {
            var criteria = new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };
            var nativeRequest = _native.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var cardigannRequest = _cardigann.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var capabilities = new IndexerCapabilities();
            capabilities.Categories.AddCategoryMapping(19, NewznabStandardCategory.Movies, "Movies");
            var nativeParser = new FunFileParser(new UserPassTorrentBaseSettings { BaseUrl = BaseUrl }, capabilities.Categories);
            var cardigannParser = new CardigannParser(Mocker.Resolve<IConfigService>(), _definition, TestLogger)
            {
                Settings = _settings
            };

            var native = (TorrentInfo)nativeParser.ParseResponse(Response(nativeRequest)).Single();
            var cardigann = (TorrentInfo)cardigannParser.ParseResponse(Response(cardigannRequest)).Single();

            cardigann.Title.Should().Be(native.Title);
            native.Guid.Should().Be(native.InfoUrl);
            cardigann.Guid.Should().Be(cardigann.DownloadUrl);
            cardigann.Guid.Should().NotBe(native.Guid);
            cardigann.InfoUrl.Should().Be(native.InfoUrl);
            cardigann.DownloadUrl.Should().Be(native.DownloadUrl);
            cardigann.Categories.Should().BeEquivalentTo(native.Categories);
            cardigann.Size.Should().Be(native.Size);
            cardigann.Files.Should().Be(native.Files);
            cardigann.Grabs.Should().Be(native.Grabs);
            cardigann.Seeders.Should().Be(native.Seeders);
            cardigann.Peers.Should().Be(native.Peers);
            cardigann.DownloadVolumeFactor.Should().Be(native.DownloadVolumeFactor);
            cardigann.UploadVolumeFactor.Should().Be(native.UploadVolumeFactor);
            cardigann.MinimumRatio.Should().Be(native.MinimumRatio);
            cardigann.MinimumSeedTime.Should().Be(native.MinimumSeedTime);
            cardigann.Genres.Should().BeEquivalentTo(native.Genres);
            cardigann.Description.Should().Be(native.Description);
            cardigann.PublishDate.Should().BeCloseTo(native.PublishDate, TimeSpan.FromSeconds(2));
        }

        [Test]
        public void candidate_definition_should_preserve_native_release_guid()
        {
            _definition.Search.Fields.Add("guid", new SelectorBlock { Text = "{{ .Result.details }}" });

            var criteria = new MovieSearchCriteria
            {
                SearchTerm = "Example Film",
                Categories = new[] { NewznabStandardCategory.Movies.Id }
            };
            var nativeRequest = _native.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var cardigannRequest = _cardigann.GetSearchRequests(criteria).GetAllTiers().Single().Single();
            var capabilities = new IndexerCapabilities();
            capabilities.Categories.AddCategoryMapping(19, NewznabStandardCategory.Movies, "Movies");
            var nativeParser = new FunFileParser(new UserPassTorrentBaseSettings { BaseUrl = BaseUrl }, capabilities.Categories);
            var cardigannParser = new CardigannParser(Mocker.Resolve<IConfigService>(), _definition, TestLogger)
            {
                Settings = _settings
            };

            var native = (TorrentInfo)nativeParser.ParseResponse(Response(nativeRequest)).Single();
            var cardigann = (TorrentInfo)cardigannParser.ParseResponse(Response(cardigannRequest)).Single();

            cardigann.Guid.Should().Be(native.Guid);
            cardigann.DownloadUrl.Should().Be(native.DownloadUrl);
        }
    }
}
