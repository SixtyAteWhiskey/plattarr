using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Upstream;

namespace NzbDrone.Core.Test.Upstream
{
    [TestFixture]
    public class SourceRowFixture : CoreTest
    {
        private static SourceRow Row(params (string Column, object Value)[] values)
        {
            var dictionary = new Dictionary<string, object>();

            foreach (var (column, value) in values)
            {
                dictionary[column] = value;
            }

            return new SourceRow(dictionary);
        }

        [Test]
        public void should_match_columns_case_insensitively()
        {
            var row = Row(("MovieId", 7L));

            row.Has("movieid").Should().BeTrue();
            row.GetInt("MOVIEID").Should().Be(7);
        }

        [Test]
        public void should_read_an_integer_as_text()
        {
            Row(("Id", 5L)).GetString("Id").Should().Be("5");
        }

        [Test]
        public void should_return_null_for_an_absent_or_null_text_column()
        {
            var row = Row(("Path", DBNull.Value));

            row.GetString("Path").Should().BeNull();
            row.GetString("Nothing").Should().BeNull();
            row.GetNullableInt("Nothing").Should().BeNull();
            row.GetJson("Nothing").Should().BeNull();
            row.IsNull("Path").Should().BeTrue();
            row.IsNull("Nothing").Should().BeTrue();
            row.Has("Path").Should().BeTrue();
            row.Has("Nothing").Should().BeFalse();
        }

        [Test]
        public void should_throw_for_a_mandatory_column_that_is_absent_or_null()
        {
            var row = Row(("Quality", DBNull.Value));

            Action absent = () => row.GetInt("Id");
            Action nullValue = () => row.GetInt("Quality");

            absent.Should().Throw<UpstreamImportException>().WithMessage("*Id*");
            nullValue.Should().Throw<UpstreamImportException>().WithMessage("*Quality*");
        }

        [Test]
        public void should_throw_when_a_value_does_not_fit_in_32_bits()
        {
            var row = Row(("Size", 3000000000L));

            Action act = () => row.GetInt("Size");

            act.Should().Throw<UpstreamImportException>().WithMessage("*Size*");
            row.GetLong("Size").Should().Be(3000000000L);
        }

        [Test]
        public void should_not_put_the_value_in_an_error_message()
        {
            var row = Row(("Password", "hunter2-secret"));

            Action act = () => row.GetInt("Password");

            act.Should().Throw<UpstreamImportException>().Which.Message.Should().NotContain("hunter2");
        }

        [TestCase(0L, false)]
        [TestCase(1L, true)]
        public void should_read_sqlite_booleans(long stored, bool expected)
        {
            Row(("Monitored", stored)).GetBool("Monitored").Should().Be(expected);
        }

        [Test]
        public void should_read_a_blob_as_utf8_text()
        {
            Row(("Settings", Encoding.UTF8.GetBytes("{\"a\":1}"))).GetString("Settings").Should().Be("{\"a\":1}");
        }

        [Test]
        public void should_parse_json_columns()
        {
            var json = Row(("Quality", "{\"quality\":{\"id\":7}}")).GetJson("Quality");

            json["quality"]["id"].Value<int>().Should().Be(7);
        }

        [Test]
        public void should_return_null_for_an_empty_json_column()
        {
            Row(("Tags", "  ")).GetJson("Tags").Should().BeNull();
        }

        [Test]
        public void should_throw_for_malformed_json_without_echoing_it()
        {
            var row = Row(("Settings", "{\"apiKey\": \"hunter2-secret\""));

            Action act = () => row.GetJson("Settings");

            act.Should().Throw<UpstreamImportException>()
                .Which.Message.Should().Contain("Settings").And.NotContain("hunter2");
        }
    }
}
