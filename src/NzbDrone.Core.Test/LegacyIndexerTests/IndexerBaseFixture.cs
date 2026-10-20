using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NLog;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Movies.Indexers.Legacy;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.LegacyIndexerTests;

[TestFixture]
public class IndexerBaseFixture : CoreTest<IndexerBase<TestIndexerSettings>>
{
    private TestIndexer _indexer;

    [SetUp]
    public void Setup()
    {
        _indexer = new TestIndexer(new Mock<IHttpClient>().Object,
            new Mock<IIndexerStatusService>().Object,
            new Mock<IConfigService>().Object,
            new Mock<IParsingService>().Object,
            new Mock<Logger>().Object)
        {
            Definition = new IndexerDefinition
            {
                Settings = new TestIndexerSettings
                {
                    MultiLanguages = new List<int> { Language.German.Id, Language.English.Id }
                }
            }
        };
    }

    [TestCase("The.Movie.Name.2016.Multi.DTS.720p.BluRay.x264-RlsGrp")]
    public void should_parse_multi_language(string postTitle)
    {
        var result = _indexer.CleanupReleases(new ReleaseInfo[] { new() { Title = postTitle, Languages = new List<string>() } });
        var languages = IndexerLanguageMapper.Map(result.Single().Languages);
        languages.Count.Should().Be(2);
        languages.Should().Contain(Language.German);
        languages.Should().Contain(Language.English);
    }
}
