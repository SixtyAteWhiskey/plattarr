using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Music;
using NzbDrone.Core.Music.MediaFiles;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MusicTests.MediaFiles.MediaFileServiceTests
{
    [TestFixture]
    public class FilterFixture : FileSystemTest<MediaFileService>
    {
        private DateTime _lastWrite = new DateTime(2019, 1, 1);

        private List<FileInfo> GivenFiles(string[] files)
        {
            foreach (var file in files)
            {
                FileSystem.AddFile(file, new MockFileData(string.Empty) { LastWriteTime = _lastWrite });
            }

            return files.Select(x => new FileInfo(x)).ToList();
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_all_files_if_no_existing_files(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    Tmp("C:\\file1.avi"),
                    Tmp("C:\\file2.avi"),
                    Tmp("C:\\file3.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>());

            Subject.FilterUnchangedFiles(files, filter).Should().BeEquivalentTo(files);
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_nothing_if_all_files_exist(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    Tmp("C:\\file1.avi"),
                    Tmp("C:\\file2.avi"),
                    Tmp("C:\\file3.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(files.Select(f => new TrackFile
                {
                    Path = f.FullName,
                    Modified = _lastWrite
                }).ToList());

            Subject.FilterUnchangedFiles(files, filter).Should().BeEmpty();
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_return_existing_files(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    Tmp("C:\\file1.avi"),
                    Tmp("C:\\file2.avi"),
                    Tmp("C:\\file3.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(Tmp("C:\\file2.avi"));
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_none_existing_files_ignoring_case(FilterFilesType filter)
        {
            WindowsOnly();

            var files = GivenFiles(new[]
                {
                    Tmp("C:\\file1.avi"),
                    Tmp("C:\\FILE2.avi"),
                    Tmp("C:\\file3.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(Tmp("C:\\file2.avi"));
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_none_existing_files_not_ignoring_case(FilterFilesType filter)
        {
            PosixOnly();

            var files = GivenFiles(new[]
                {
                    Tmp("C:\\file1.avi"),
                    Tmp("C:\\FILE2.avi"),
                    Tmp("C:\\file3.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_change_casing(FilterFilesType filter)
        {
            var files = GivenFiles(new[]
                {
                    Tmp("C:\\FILE1.avi")
                });

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>());

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(1);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(files.First().FullName.ToLower());
            Subject.FilterUnchangedFiles(files, filter).Should().Contain(files.First());
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_not_return_existing_file_if_size_unchanged(FilterFilesType filter)
        {
            FileSystem.AddFile(Tmp("C:\\file1.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file2.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file3.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => new FileInfo(x)).ToList();

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Size = 10,
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(Tmp("C:\\file2.avi"));
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_return_existing_file_if_unmatched(FilterFilesType filter)
        {
            FileSystem.AddFile(Tmp("C:\\file1.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file2.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file3.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => new FileInfo(x)).ToList();

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Size = 10,
                        Modified = _lastWrite,
                        Tracks = new List<Track>()
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain(Tmp("C:\\file2.avi"));
        }

        [TestCase(FilterFilesType.Matched)]
        public void filter_unmatched_should_not_return_existing_file_if_matched(FilterFilesType filter)
        {
            FileSystem.AddFile(Tmp("C:\\file1.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file2.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file3.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => new FileInfo(x)).ToList();

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Size = 10,
                        Modified = _lastWrite,
                        Tracks = Builder<Track>.CreateListOfSize(1).Build() as List<Track>
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(2);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().NotContain(Tmp("C:\\file2.avi"));
        }

        [TestCase(FilterFilesType.Known)]
        [TestCase(FilterFilesType.Matched)]
        public void filter_should_return_existing_file_if_size_changed(FilterFilesType filter)
        {
            FileSystem.AddFile(Tmp("C:\\file1.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file2.avi"), new MockFileData("".PadRight(11)) { LastWriteTime = _lastWrite });
            FileSystem.AddFile(Tmp("C:\\file3.avi"), new MockFileData("".PadRight(10)) { LastWriteTime = _lastWrite });

            var files = FileSystem.AllFiles.Select(x => new FileInfo(x)).ToList();

            Mocker.GetMock<ITrackFileRepository>()
                .Setup(c => c.GetFileWithPath(It.IsAny<List<string>>()))
                .Returns(new List<TrackFile>
                {
                    new TrackFile
                    {
                        Path = Tmp("C:\\file2.avi"),
                        Size = 10,
                        Modified = _lastWrite
                    }
                });

            Subject.FilterUnchangedFiles(files, filter).Should().HaveCount(3);
            Subject.FilterUnchangedFiles(files, filter).Select(x => x.FullName).Should().Contain(Tmp("C:\\file2.avi"));
        }
    }
}
