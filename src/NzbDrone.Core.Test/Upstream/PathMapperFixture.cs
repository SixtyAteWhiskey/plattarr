using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class PathMapperFixture : CoreTest
    {
        private static PathMapper Mapper(params (string From, string To)[] rules)
        {
            var map = new Dictionary<string, string>();

            foreach (var (from, to) in rules)
            {
                map[from] = to;
            }

            return new PathMapper(map);
        }

        [TestCase("/movies/Sintel (2010)", "/data/media/movies/Sintel (2010)")]
        [TestCase("/movies", "/data/media/movies")]
        [TestCase("/movies/", "/data/media/movies/")]
        [TestCase("/downloads/complete/Sintel", "/data/torrents/complete/Sintel")]
        public void should_rewrite_the_matching_prefix(string path, string expected)
        {
            var mapper = Mapper(("/movies", "/data/media/movies"), ("/downloads", "/data/torrents"));

            mapper.Map(path).Should().Be(expected);
        }

        [Test]
        public void should_prefer_the_longest_prefix()
        {
            var mapper = Mapper(("/media", "/data/media"), ("/media/movies", "/data/films"));

            mapper.Map("/media/movies/Sintel").Should().Be("/data/films/Sintel");
            mapper.Map("/media/shows/Sintel").Should().Be("/data/media/shows/Sintel");
        }

        [Test]
        public void should_match_whole_path_segments_only()
        {
            var mapper = Mapper(("/movies", "/data/movies"));

            mapper.Map("/movies2/Sintel").Should().Be("/movies2/Sintel");
            mapper.Map("/moviesXL").Should().Be("/moviesXL");
        }

        [Test]
        public void should_ignore_trailing_separators_on_either_side_of_a_rule()
        {
            var mapper = Mapper(("/movies/", "/data/movies/"));

            mapper.Map("/movies/Sintel").Should().Be("/data/movies/Sintel");
            mapper.Map("/movies").Should().Be("/data/movies");
        }

        [Test]
        public void should_leave_other_paths_alone()
        {
            var mapper = Mapper(("/movies", "/data/movies"));

            mapper.Map("/shows/Sintel").Should().Be("/shows/Sintel");
            mapper.Map("Sintel (2010)/Sintel.mkv").Should().Be("Sintel (2010)/Sintel.mkv");
        }

        [Test]
        public void should_pass_null_and_empty_through()
        {
            var mapper = Mapper(("/movies", "/data/movies"));

            mapper.Map(null).Should().BeNull();
            mapper.Map(string.Empty).Should().BeEmpty();
        }

        [Test]
        public void should_be_case_sensitive()
        {
            var mapper = Mapper(("/movies", "/data/movies"));

            mapper.Map("/Movies/Sintel").Should().Be("/Movies/Sintel");
        }

        [Test]
        public void should_map_a_windows_source_onto_a_linux_target()
        {
            var mapper = Mapper((@"D:\Movies", "/data/media/movies"));

            mapper.Map(@"D:\Movies\Sintel (2010)\Sintel.mkv").Should().Be("/data/media/movies/Sintel (2010)/Sintel.mkv");
        }

        [Test]
        public void should_map_a_linux_source_onto_a_windows_target()
        {
            var mapper = Mapper(("/movies", @"E:\Media\Movies"));

            mapper.Map("/movies/Sintel (2010)").Should().Be(@"E:\Media\Movies\Sintel (2010)");
        }

        [Test]
        public void should_map_the_root_when_a_rule_names_it()
        {
            var mapper = Mapper(("/", "/data"));

            mapper.Map("/movies/Sintel").Should().Be("/data/movies/Sintel");
            mapper.Map("relative/path").Should().Be("relative/path");
        }

        [Test]
        public void should_refuse_a_source_path_listed_twice()
        {
            Action create = () => Mapper(("/movies", "/a"), ("/movies/", "/b"));

            create.Should().Throw<UpstreamImportException>();
        }

        [Test]
        public void should_accept_no_rules()
        {
            new PathMapper(null).Map("/movies/Sintel").Should().Be("/movies/Sintel");
            new PathMapper(new Dictionary<string, string>()).Map("/movies/Sintel").Should().Be("/movies/Sintel");
        }
    }
}
