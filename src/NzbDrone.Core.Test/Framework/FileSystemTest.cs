using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Framework
{
    // The content and timestamp of a file a test puts on the temporary file system.
    public class MockFileData
    {
        public MockFileData(string contents)
        {
            Contents = contents ?? string.Empty;
            LastWriteTime = DateTimeOffset.UtcNow;
        }

        public string Contents { get; }

        public DateTimeOffset LastWriteTime { get; set; }

        public FileShare AllowedFileShare { get; set; } = FileShare.ReadWrite | FileShare.Delete;
    }

    // A real folder per test that stands in for the drive a Lidarr test builds in memory. Plattarr
    // reads files through FileInfo and DirectoryInfo, which cannot be mocked, so a test that needs file
    // sizes, timestamps or a folder listing creates real files here. Tmp maps the "C:\Test\Music" paths
    // the ported tests use to a path under that folder; the code under test sees only mapped paths.
    public class TempFileSystem : IDisposable
    {
        private readonly string _root;
        private readonly List<string> _files = new List<string>();
        private readonly List<FileStream> _locks = new List<FileStream>();

        public TempFileSystem(string root)
        {
            _root = root;
        }

        public IEnumerable<string> AllFiles => _files.ToList();

        public string Real(string path)
        {
            if (path.StartsWith(_root, StringComparison.Ordinal))
            {
                return path;
            }

            var mapped = path.AsOsAgnostic();

            if (OsInfo.IsWindows && mapped.Length > 1 && mapped[1] == ':')
            {
                mapped = mapped.Remove(1, 1);
            }

            return Path.Combine(_root, mapped.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        public void AddDirectory(string path)
        {
            Directory.CreateDirectory(Real(path));
        }

        public void AddFile(string path, MockFileData data)
        {
            var real = Real(path);

            Directory.CreateDirectory(Path.GetDirectoryName(real));
            File.WriteAllText(real, data.Contents, new UTF8Encoding(false));
            File.SetLastWriteTimeUtc(real, data.LastWriteTime.UtcDateTime);

            if (!_files.Contains(real))
            {
                _files.Add(real);
            }

            // A file another process holds open: keep it open with no sharing until the test ends.
            if (data.AllowedFileShare == FileShare.None)
            {
                _locks.Add(new FileStream(real, FileMode.Open, FileAccess.Read, FileShare.None));
            }
        }

        public void Dispose()
        {
            foreach (var held in _locks)
            {
                held.Dispose();
            }

            _locks.Clear();
        }
    }

    // The file operations of the platform disk providers without the platform parts (permissions,
    // hard links, free space), so a test runs the real DiskProviderBase over the temporary folder.
    public class TestDiskProvider : DiskProviderBase
    {
        public override long? GetAvailableSpace(string path) => long.MaxValue;

        public override void InheritFolderPermissions(string filename)
        {
        }

        public override void SetEveryonePermissions(string filename)
        {
        }

        public override void SetFilePermissions(string path, string mask, string group)
        {
        }

        public override void SetPermissions(string path, string mask, string group)
        {
        }

        public override void CopyPermissions(string sourcePath, string targetPath)
        {
        }

        public override long? GetTotalSize(string path) => long.MaxValue;

        public override bool TryCreateHardLink(string source, string destination) => false;
    }

    public abstract class FileSystemTest<TSubject> : CoreTest<TSubject>
        where TSubject : class
    {
        protected TempFileSystem FileSystem { get; private set; }
        protected IDiskProvider DiskProvider { get; private set; }

        [SetUp]
        public void FileSystemTestSetup()
        {
            FileSystem = new TempFileSystem(TempFolder);
            DiskProvider = new TestDiskProvider();

            Mocker.SetConstant(DiskProvider);
        }

        [TearDown]
        public void FileSystemTestTearDown()
        {
            FileSystem?.Dispose();
        }

        // The path a ported "C:\Test\Music" style path has on the temporary file system.
        protected string Tmp(string path)
        {
            return FileSystem.Real(path);
        }
    }
}
