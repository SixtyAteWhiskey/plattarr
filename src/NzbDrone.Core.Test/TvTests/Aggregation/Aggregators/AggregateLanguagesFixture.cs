using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Overrides;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Modules;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Aggregation.Aggregators;
using NzbDrone.Core.Tv.Parser.Model;

namespace NzbDrone.Core.Test.TvTests.Aggregation.Aggregators
{
    [TestFixture]
    public class AggregateLanguagesFixture : CoreTest<AggregateLanguages>
    {
        private RemoteEpisode _remoteEpisode;
        private Series _series;
        private Episode _episode;
        private string _simpleReleaseTitle = "Series.Title.S01E01.xyz-RlsGroup";

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                       .With(s => s.Title = "Some Series")
                       .With(s => s.OriginalLanguage = Language.English)
                       .Build();

            _episode = Builder<Episode>.CreateNew()
                       .With(e => e.Title = "Some Episode")
                       .Build();

            _remoteEpisode = Builder<RemoteEpisode>.CreateNew()
                                                 .With(l => l.ParsedEpisodeInfo = null)
                                                 .With(l => l.Series = _series)
                                                 .With(l => l.Episodes = new List<Episode> { _episode })
                                                 .With(l => l.Release = new ReleaseInfo())
                                                 .Build();

            // The real service returns an empty override for indexers without one.
            Mocker.GetMock<IIndexerOverrideService>()
                .Setup(v => v.Get(It.IsAny<int>(), It.IsAny<MediaKind>()))
                .Returns<int, MediaKind>((id, kind) => new IndexerOverride { IndexerId = id, MediaKind = kind });
        }

        private ParsedEpisodeInfo GetParsedEpisodeInfo(List<Language> languages, string releaseTitle, string releaseTokens = "")
        {
            return new ParsedEpisodeInfo
            {
                Languages = languages,
                ReleaseTitle = releaseTitle,
                ReleaseTokens = releaseTokens
            };
        }

        private void GivenMultiLanguages(int indexerId, params int[] languageIds)
        {
            Mocker.GetMock<IIndexerOverrideService>()
                .Setup(v => v.Get(indexerId, MediaKind.Tv))
                .Returns(new IndexerOverride { IndexerId = indexerId, MediaKind = MediaKind.Tv, MultiLanguages = new List<int>(languageIds) });
        }

        [Test]
        public void should_return_existing_language_if_episode_title_does_not_have_language()
        {
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Original }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Contain(_series.OriginalLanguage);
        }

        [Test]
        public void should_return_parsed_language()
        {
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.French }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(_remoteEpisode.ParsedEpisodeInfo.Languages);
        }

        [Test]
        public void should_return_multi_languages_when_indexer_id_has_multi_languages_configuration()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition);

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_from_indexer_with_id_when_indexer_id_and_name_are_set()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition1 = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer1",
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            var indexerDefinition2 = new IndexerDefinition
            {
                Id = 2,
                Name = "MyIndexer2",
            };
            GivenMultiLanguages(2, Language.Original.Id, Language.German.Id);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition1);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition>() { indexerDefinition1, indexerDefinition2 });

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Indexer = "MyIndexer2";
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_when_indexer_name_has_multi_languages_configuration()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer (Prowlarr)",
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition> { indexerDefinition });

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.Indexer = "MyIndexer (Prowlarr)";
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.All(), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_when_release_as_unknown_as_default_language_and_indexer_has_multi_languages_configuration()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition);

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Unknown }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_when_release_as_specified_language_and_indexer_has_multi_languages_configuration()
        {
            var releaseTitle = "Some.Series.S01E01.MULTi.VFF.VFQ.1080p.BluRay.DTS.HDMA.x264-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition);

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.French }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_when_release_as_other_language_and_indexer_has_multi_languages_configuration()
        {
            var releaseTitle = "Some.Series.S01E01.MULTi.GERMAN.1080p.BluRay.DTS.HDMA.x264-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition);

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.German }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French, Language.German });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_original_when_indexer_has_no_multi_languages_configuration()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition = new IndexerDefinition
            {
                Id = 1,
            };
            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(indexerDefinition);

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 1;
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(1), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_original_when_no_indexer_value()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage });
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_exclude_language_that_is_part_of_episode_title_when_release_tokens_contains_episode_title()
        {
            var releaseTitle = "Series.Title.S01E01.Jimmy.The.Greek.xyz-RlsGroup";
            var releaseTokens = ".Jimmy.The.Greek.xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(_series.OriginalLanguage);
        }

        [Test]
        public void should_remove_parsed_language_that_is_part_of_episode_title_when_release_tokens_contains_episode_title()
        {
            var releaseTitle = "Series.Title.S01E01.Jimmy.The.Greek.French.xyz-RlsGroup";
            var releaseTokens = ".Jimmy.The.Greek.French.xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek, Language.French }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.French);
        }

        [Test]
        public void should_not_exclude_language_that_is_part_of_episode_title_when_release_tokens_does_not_contain_episode_title()
        {
            var releaseTitle = "Series.Title.S01E01.xyz-RlsGroup";
            var releaseTokens = ".xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.Greek);
        }

        [Test]
        public void should_use_reparse_language_after_determining_languages_that_are_in_episode_titles()
        {
            var releaseTitle = "Series.Title.S01E01.Jimmy.The.Greek.Greek.xyz-RlsGroup";
            var releaseTokens = ".Jimmy.The.Greek.Greek.xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.Greek);
        }

        [Test]
        public void should_return_multi_languages_from_indexer_with_name_when_indexer_id_does_not_exist()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition1 = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer1",
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            var indexerDefinition2 = new IndexerDefinition
            {
                Id = 2,
                Name = "MyIndexer2",
            };
            GivenMultiLanguages(2, Language.Original.Id, Language.German.Id);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(null as IndexerDefinition);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition> { indexerDefinition1 });

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition>() { indexerDefinition1, indexerDefinition2 });

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 10;
            _remoteEpisode.Release.Indexer = "MyIndexer1";
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(10), Times.Once());
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.All(), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_return_multi_languages_from_indexer_with_name_when_indexer_id_not_available()
        {
            var releaseTitle = "Series.Title.S01E01.MULTi.1080p.WEB.H265-RlsGroup";
            var indexerDefinition1 = new IndexerDefinition
            {
                Id = 1,
                Name = "MyIndexer1",
            };
            GivenMultiLanguages(1, Language.Original.Id, Language.French.Id);
            var indexerDefinition2 = new IndexerDefinition
            {
                Id = 2,
                Name = "MyIndexer2",
            };
            GivenMultiLanguages(2, Language.Original.Id, Language.German.Id);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.Find(1))
                .Returns(null as IndexerDefinition);

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition> { indexerDefinition1 });

            Mocker.GetMock<IIndexerFactory>()
                .Setup(v => v.All())
                .Returns(new List<IndexerDefinition>() { indexerDefinition1, indexerDefinition2 });

            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { }, releaseTitle);
            _remoteEpisode.Release.IndexerId = 0;
            _remoteEpisode.Release.Indexer = "MyIndexer1";
            _remoteEpisode.Release.Title = releaseTitle;

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.French });
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.Find(10), Times.Never());
            Mocker.GetMock<IIndexerFactory>().Verify(c => c.All(), Times.Once());
            Mocker.GetMock<IIndexerFactory>().VerifyNoOtherCalls();
        }

        [Test]
        public void should_use_the_languages_the_indexer_provided()
        {
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.French }, _simpleReleaseTitle);
            _remoteEpisode.Release.Languages = new List<string> { "German" };

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.German);
        }

        [Test]
        public void should_use_the_parsed_languages_when_the_series_is_unknown()
        {
            _remoteEpisode.Series = null;
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.French }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.French);
        }

        [Test]
        public void should_use_the_indexer_languages_when_the_series_is_unknown()
        {
            _remoteEpisode.Series = null;
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.French }, _simpleReleaseTitle);
            _remoteEpisode.Release.Languages = new List<string> { "German" };

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.German);
        }

        [Test]
        public void should_fall_back_to_the_series_original_language_when_none_is_parsed()
        {
            _series.OriginalLanguage = Language.Japanese;
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Unknown }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.Japanese);
        }

        [Test]
        public void should_exclude_a_language_found_in_any_of_the_episode_titles()
        {
            var releaseTitle = "Series.Title.S01E01E02.Jimmy.The.Greek.xyz-RlsGroup";
            var releaseTokens = ".Jimmy.The.Greek.xyz-RlsGroup";

            _remoteEpisode.Episodes = new List<Episode>
            {
                new Episode { Title = "Something Else" },
                new Episode { Title = "Jimmy The Greek" }
            };
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(_series.OriginalLanguage);
        }

        [Test]
        public void should_read_the_release_title_when_there_are_no_release_tokens()
        {
            var releaseTitle = "Series.Title.S01E01.Jimmy.The.Greek.xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = new ParsedEpisodeInfo { Languages = new List<Language> { Language.Greek }, ReleaseTitle = releaseTitle };

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(_series.OriginalLanguage);
        }

        [Test]
        public void should_prefer_the_release_tokens_over_the_release_title_when_looking_for_the_episode_title()
        {
            var releaseTitle = "Series.Title.S01E01.Jimmy.The.Greek.xyz-RlsGroup";
            var releaseTokens = ".xyz-RlsGroup";

            _episode.Title = "Jimmy The Greek";
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Greek }, releaseTitle, releaseTokens);

            Subject.Aggregate(_remoteEpisode).Languages.Should().Equal(Language.Greek);
        }

        [Test]
        public void should_replace_original_with_the_series_original_language()
        {
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Original, Language.French }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { Language.French, _series.OriginalLanguage });
        }

        [Test]
        public void should_replace_original_with_unknown_when_the_series_original_language_is_already_listed()
        {
            _remoteEpisode.ParsedEpisodeInfo = GetParsedEpisodeInfo(new List<Language> { Language.Original, _series.OriginalLanguage }, _simpleReleaseTitle);

            Subject.Aggregate(_remoteEpisode).Languages.Should().BeEquivalentTo(new List<Language> { _series.OriginalLanguage, Language.Unknown });
        }
    }
}
