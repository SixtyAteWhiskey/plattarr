using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Languages
{
    [TestFixture]
    public class LanguageFixture : CoreTest
    {
        // Catalog IDs are stored in every database; they must never change
        // (docs/catalogs.md). These cases pin every one.
        public static object[] FromIntCases =
            {
                new object[] { -2, Language.Original },
                new object[] { -1, Language.Any },
                new object[] { 0, Language.Unknown },
                new object[] { 1, Language.Afrikaans },
                new object[] { 2, Language.Albanian },
                new object[] { 3, Language.Arabic },
                new object[] { 4, Language.Azerbaijani },
                new object[] { 5, Language.Bengali },
                new object[] { 6, Language.Bosnian },
                new object[] { 7, Language.Bulgarian },
                new object[] { 8, Language.Catalan },
                new object[] { 9, Language.Chinese },
                new object[] { 10, Language.Croatian },
                new object[] { 11, Language.Czech },
                new object[] { 12, Language.Danish },
                new object[] { 13, Language.Dutch },
                new object[] { 14, Language.English },
                new object[] { 15, Language.Estonian },
                new object[] { 16, Language.Finnish },
                new object[] { 17, Language.Flemish },
                new object[] { 18, Language.French },
                new object[] { 19, Language.Georgian },
                new object[] { 20, Language.German },
                new object[] { 21, Language.Greek },
                new object[] { 22, Language.Hebrew },
                new object[] { 23, Language.Hindi },
                new object[] { 24, Language.Hungarian },
                new object[] { 25, Language.Icelandic },
                new object[] { 26, Language.Indonesian },
                new object[] { 27, Language.Irish },
                new object[] { 28, Language.Italian },
                new object[] { 29, Language.Japanese },
                new object[] { 30, Language.Kannada },
                new object[] { 31, Language.Korean },
                new object[] { 32, Language.Latvian },
                new object[] { 33, Language.Lithuanian },
                new object[] { 34, Language.Macedonian },
                new object[] { 35, Language.Malay },
                new object[] { 36, Language.Malayalam },
                new object[] { 37, Language.Marathi },
                new object[] { 38, Language.Mongolian },
                new object[] { 39, Language.Norwegian },
                new object[] { 40, Language.Persian },
                new object[] { 41, Language.Polish },
                new object[] { 42, Language.Portuguese },
                new object[] { 43, Language.PortugueseBR },
                new object[] { 44, Language.Romanian },
                new object[] { 45, Language.Romansh },
                new object[] { 46, Language.Russian },
                new object[] { 47, Language.Serbian },
                new object[] { 48, Language.Slovak },
                new object[] { 49, Language.Slovenian },
                new object[] { 50, Language.Spanish },
                new object[] { 51, Language.SpanishLatino },
                new object[] { 52, Language.Swedish },
                new object[] { 53, Language.Tagalog },
                new object[] { 54, Language.Tamil },
                new object[] { 55, Language.Telugu },
                new object[] { 56, Language.Thai },
                new object[] { 57, Language.Turkish },
                new object[] { 58, Language.Ukrainian },
                new object[] { 59, Language.Urdu },
                new object[] { 60, Language.Uzbek },
                new object[] { 61, Language.Vietnamese },
            };

        public static object[] ToIntCases =
            {
                new object[] { Language.Original, -2 },
                new object[] { Language.Any, -1 },
                new object[] { Language.Unknown, 0 },
                new object[] { Language.Afrikaans, 1 },
                new object[] { Language.Albanian, 2 },
                new object[] { Language.Arabic, 3 },
                new object[] { Language.Azerbaijani, 4 },
                new object[] { Language.Bengali, 5 },
                new object[] { Language.Bosnian, 6 },
                new object[] { Language.Bulgarian, 7 },
                new object[] { Language.Catalan, 8 },
                new object[] { Language.Chinese, 9 },
                new object[] { Language.Croatian, 10 },
                new object[] { Language.Czech, 11 },
                new object[] { Language.Danish, 12 },
                new object[] { Language.Dutch, 13 },
                new object[] { Language.English, 14 },
                new object[] { Language.Estonian, 15 },
                new object[] { Language.Finnish, 16 },
                new object[] { Language.Flemish, 17 },
                new object[] { Language.French, 18 },
                new object[] { Language.Georgian, 19 },
                new object[] { Language.German, 20 },
                new object[] { Language.Greek, 21 },
                new object[] { Language.Hebrew, 22 },
                new object[] { Language.Hindi, 23 },
                new object[] { Language.Hungarian, 24 },
                new object[] { Language.Icelandic, 25 },
                new object[] { Language.Indonesian, 26 },
                new object[] { Language.Irish, 27 },
                new object[] { Language.Italian, 28 },
                new object[] { Language.Japanese, 29 },
                new object[] { Language.Kannada, 30 },
                new object[] { Language.Korean, 31 },
                new object[] { Language.Latvian, 32 },
                new object[] { Language.Lithuanian, 33 },
                new object[] { Language.Macedonian, 34 },
                new object[] { Language.Malay, 35 },
                new object[] { Language.Malayalam, 36 },
                new object[] { Language.Marathi, 37 },
                new object[] { Language.Mongolian, 38 },
                new object[] { Language.Norwegian, 39 },
                new object[] { Language.Persian, 40 },
                new object[] { Language.Polish, 41 },
                new object[] { Language.Portuguese, 42 },
                new object[] { Language.PortugueseBR, 43 },
                new object[] { Language.Romanian, 44 },
                new object[] { Language.Romansh, 45 },
                new object[] { Language.Russian, 46 },
                new object[] { Language.Serbian, 47 },
                new object[] { Language.Slovak, 48 },
                new object[] { Language.Slovenian, 49 },
                new object[] { Language.Spanish, 50 },
                new object[] { Language.SpanishLatino, 51 },
                new object[] { Language.Swedish, 52 },
                new object[] { Language.Tagalog, 53 },
                new object[] { Language.Tamil, 54 },
                new object[] { Language.Telugu, 55 },
                new object[] { Language.Thai, 56 },
                new object[] { Language.Turkish, 57 },
                new object[] { Language.Ukrainian, 58 },
                new object[] { Language.Urdu, 59 },
                new object[] { Language.Uzbek, 60 },
                new object[] { Language.Vietnamese, 61 },
            };

        [Test]
        [TestCaseSource("FromIntCases")]
        public void should_be_able_to_convert_int_to_languageTypes(int source, Language expected)
        {
            var language = (Language)source;
            language.Should().Be(expected);
        }

        [Test]
        [TestCaseSource("ToIntCases")]
        public void should_be_able_to_convert_languageTypes_to_int(Language source, int expected)
        {
            var i = (int)source;
            i.Should().Be(expected);
        }
    }
}
