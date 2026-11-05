using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Hygiene;

namespace NzbDrone.Core.Test.QueueTests
{
    [TestFixture]
    public class HygieneContentPolicyFixture
    {
        [Test]
        public void blocklist_keeps_mixed_torrent_and_identifies_files_to_skip_by_default()
        {
            var result = HygieneContentPolicy.Inspect(new[] { "Movie/video.mkv", "Movie/setup.EXE" }, "*.exe", "blocklist", false);

            result.Status.Should().Be("skip-blocked-files");
            result.WouldRemoveDownload.Should().BeFalse();
            result.BlockedFiles.Should().ContainSingle().Which.Should().Be("Movie/setup.EXE");
            result.AllowedFiles.Should().ContainSingle().Which.Should().Be("Movie/video.mkv");
        }

        [Test]
        public void strict_mixed_policy_recommends_whole_download_removal()
        {
            var result = HygieneContentPolicy.Inspect(new[] { "Movie/video.mkv", "Movie/setup.exe" }, "*.exe", "blocklist", true);

            result.Status.Should().Be("remove-download");
            result.WouldRemoveDownload.Should().BeTrue();
        }

        [Test]
        public void allowlist_recommends_removal_when_every_file_is_blocked()
        {
            var result = HygieneContentPolicy.Inspect(new[] { "Movie/setup.exe" }, "*.mkv\n*.srt", "allowlist", false);

            result.Status.Should().Be("remove-download");
            result.BlockedFiles.Should().ContainSingle();
        }

        [Test]
        public void regex_and_exact_name_patterns_work_on_file_names()
        {
            var result = HygieneContentPolicy.Inspect(
                new[] { "Folder/readme.txt", "Folder/virus123.exe" },
                "readme.txt\nregex:^virus[0-9]+\\.exe$",
                "blocklist",
                false);

            result.BlockedFiles.Should().HaveCount(2);
        }

        [Test]
        public void missing_file_list_never_recommends_removal()
        {
            var result = HygieneContentPolicy.Inspect(null, "*.exe", "blocklist", true);

            result.Status.Should().Be("files-unavailable");
            result.WouldRemoveDownload.Should().BeFalse();
        }
    }
}
