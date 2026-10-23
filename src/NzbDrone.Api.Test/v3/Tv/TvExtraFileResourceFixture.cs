using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Tv.Extras.Files;
using NzbDrone.Core.Tv.Extras.Metadata.Files;
using NzbDrone.Core.Tv.Extras.Others;
using NzbDrone.Core.Tv.Extras.Subtitles;
using NzbDrone.Test.Common;
using Radarr.Api.V3.Tv.ExtraFiles;
using ExtraFileType = NzbDrone.Core.Movies.Extras.Files.ExtraFileType;

namespace NzbDrone.Api.Test.v3.Tv
{
    [TestFixture]
    public class TvExtraFileResourceFixture : TestBase
    {
        [Test]
        public void should_map_a_subtitle_with_its_languages_and_title()
        {
            var resource = new SubtitleFile
            {
                Id = 1,
                SeriesId = 3,
                SeasonNumber = 1,
                EpisodeFileId = 40,
                RelativePath = "Season 01/Show - S01E01.en.srt",
                Extension = ".srt",
                LanguageTags = new List<string> { "forced" },
                Title = "English"
            }.ToResource();

            resource.Type.Should().Be(ExtraFileType.Subtitle);
            resource.SeriesId.Should().Be(3);
            resource.SeasonNumber.Should().Be(1);
            resource.EpisodeFileId.Should().Be(40);
            resource.RelativePath.Should().Be("Season 01/Show - S01E01.en.srt");
            resource.Extension.Should().Be(".srt");
            resource.LanguageTags.Should().Equal("forced");
            resource.Title.Should().Be("English");
        }

        [Test]
        public void should_map_metadata_and_other_files_without_language_fields()
        {
            var metadata = new MetadataFile { Id = 2, SeriesId = 3, RelativePath = "tvshow.nfo", Extension = ".nfo" }.ToResource();
            var other = new OtherExtraFile { Id = 3, SeriesId = 3, SeasonNumber = 2, EpisodeFileId = null, RelativePath = "Season 02/extras.txt", Extension = ".txt" }.ToResource();

            metadata.Type.Should().Be(ExtraFileType.Metadata);
            metadata.LanguageTags.Should().BeNull();
            metadata.EpisodeFileId.Should().BeNull();
            other.Type.Should().Be(ExtraFileType.Other);
            other.SeasonNumber.Should().Be(2);
            other.Title.Should().BeNull();
        }

        [Test]
        public void should_map_nothing_to_null()
        {
            ((SubtitleFile)null).ToResource().Should().BeNull();
            ((MetadataFile)null).ToResource().Should().BeNull();
            ((OtherExtraFile)null).ToResource().Should().BeNull();
        }

        [Test]
        public void should_list_subtitles_then_metadata_then_other_files_each_ordered_by_path()
        {
            var subtitles = new Mock<IExtraFileService<SubtitleFile>>();
            var metadata = new Mock<IExtraFileService<MetadataFile>>();
            var others = new Mock<IExtraFileService<OtherExtraFile>>();

            subtitles.Setup(s => s.GetFilesBySeries(3)).Returns(new List<SubtitleFile> { new() { Id = 1, RelativePath = "b.srt" }, new() { Id = 2, RelativePath = "a.srt" } });
            metadata.Setup(s => s.GetFilesBySeries(3)).Returns(new List<MetadataFile> { new() { Id = 3, RelativePath = "tvshow.nfo" } });
            others.Setup(s => s.GetFilesBySeries(3)).Returns(new List<OtherExtraFile> { new() { Id = 4, RelativePath = "z.txt" }, new() { Id = 5, RelativePath = "y.txt" } });

            var result = new TvExtraFileController(subtitles.Object, metadata.Object, others.Object).GetFiles(3);

            var files = result.Should().BeOfType<Ok<List<TvExtraFileResource>>>().Subject.Value;

            files.Select(f => f.Id).Should().Equal(2, 1, 3, 5, 4);
            files.Select(f => f.Type).Should().Equal(ExtraFileType.Subtitle, ExtraFileType.Subtitle, ExtraFileType.Metadata, ExtraFileType.Other, ExtraFileType.Other);
        }

        [Test]
        public void should_ask_each_service_for_the_series_asked_about_only()
        {
            var subtitles = new Mock<IExtraFileService<SubtitleFile>>();
            var metadata = new Mock<IExtraFileService<MetadataFile>>();
            var others = new Mock<IExtraFileService<OtherExtraFile>>();

            subtitles.Setup(s => s.GetFilesBySeries(It.IsAny<int>())).Returns(new List<SubtitleFile>());
            metadata.Setup(s => s.GetFilesBySeries(It.IsAny<int>())).Returns(new List<MetadataFile>());
            others.Setup(s => s.GetFilesBySeries(It.IsAny<int>())).Returns(new List<OtherExtraFile>());

            new TvExtraFileController(subtitles.Object, metadata.Object, others.Object).GetFiles(7);

            subtitles.Verify(s => s.GetFilesBySeries(7), Times.Once());
            metadata.Verify(s => s.GetFilesBySeries(7), Times.Once());
            others.Verify(s => s.GetFilesBySeries(7), Times.Once());
        }
    }
}
