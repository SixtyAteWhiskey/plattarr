using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.Languages
{
    public class Language : IEmbeddedDocument, IEquatable<Language>
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public Language()
        {
        }

        private Language(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public override string ToString()
        {
            return Name;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public bool Equals(Language other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id.Equals(other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return Equals(obj as Language);
        }

        public static bool operator ==(Language left, Language right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Language left, Language right)
        {
            return !Equals(left, right);
        }

        // Plattarr's language catalog (docs/catalogs.md). 0 Unknown, -1 Any, and
        // -2 Original keep upstream's values; real languages were numbered from 1 in
        // alphabetical order of their English names, and new ones append. Upstream
        // IDs translate through Languages.Upstream.
        public static Language Unknown => new Language(0, "Unknown");
        public static Language English => new Language(14, "English");
        public static Language French => new Language(18, "French");
        public static Language Spanish => new Language(50, "Spanish");
        public static Language German => new Language(20, "German");
        public static Language Italian => new Language(28, "Italian");
        public static Language Danish => new Language(12, "Danish");
        public static Language Dutch => new Language(13, "Dutch");
        public static Language Japanese => new Language(29, "Japanese");
        public static Language Icelandic => new Language(25, "Icelandic");
        public static Language Chinese => new Language(9, "Chinese");
        public static Language Russian => new Language(46, "Russian");
        public static Language Polish => new Language(41, "Polish");
        public static Language Vietnamese => new Language(61, "Vietnamese");
        public static Language Swedish => new Language(52, "Swedish");
        public static Language Norwegian => new Language(39, "Norwegian");
        public static Language Finnish => new Language(16, "Finnish");
        public static Language Turkish => new Language(57, "Turkish");
        public static Language Portuguese => new Language(42, "Portuguese");
        public static Language Flemish => new Language(17, "Flemish");
        public static Language Greek => new Language(21, "Greek");
        public static Language Korean => new Language(31, "Korean");
        public static Language Hungarian => new Language(24, "Hungarian");
        public static Language Hebrew => new Language(22, "Hebrew");
        public static Language Lithuanian => new Language(33, "Lithuanian");
        public static Language Czech => new Language(11, "Czech");
        public static Language Hindi => new Language(23, "Hindi");
        public static Language Romanian => new Language(44, "Romanian");
        public static Language Thai => new Language(56, "Thai");
        public static Language Bulgarian => new Language(7, "Bulgarian");
        public static Language PortugueseBR => new Language(43, "Portuguese (Brazil)");
        public static Language Arabic => new Language(3, "Arabic");
        public static Language Ukrainian => new Language(58, "Ukrainian");
        public static Language Persian => new Language(40, "Persian");
        public static Language Bengali => new Language(5, "Bengali");
        public static Language Slovak => new Language(48, "Slovak");
        public static Language Latvian => new Language(32, "Latvian");
        public static Language SpanishLatino => new Language(51, "Spanish (Latino)");
        public static Language Catalan => new Language(8, "Catalan");
        public static Language Croatian => new Language(10, "Croatian");
        public static Language Serbian => new Language(47, "Serbian");
        public static Language Bosnian => new Language(6, "Bosnian");
        public static Language Estonian => new Language(15, "Estonian");
        public static Language Tamil => new Language(54, "Tamil");
        public static Language Indonesian => new Language(26, "Indonesian");
        public static Language Telugu => new Language(55, "Telugu");
        public static Language Macedonian => new Language(34, "Macedonian");
        public static Language Slovenian => new Language(49, "Slovenian");
        public static Language Malayalam => new Language(36, "Malayalam");
        public static Language Kannada => new Language(30, "Kannada");
        public static Language Albanian => new Language(2, "Albanian");
        public static Language Afrikaans => new Language(1, "Afrikaans");
        public static Language Marathi => new Language(37, "Marathi");
        public static Language Tagalog => new Language(53, "Tagalog");
        public static Language Urdu => new Language(59, "Urdu");
        public static Language Romansh => new Language(45, "Romansh");
        public static Language Mongolian => new Language(38, "Mongolian");
        public static Language Georgian => new Language(19, "Georgian");
        public static Language Azerbaijani => new Language(4, "Azerbaijani");
        public static Language Uzbek => new Language(60, "Uzbek");
        public static Language Malay => new Language(35, "Malay");
        public static Language Irish => new Language(27, "Irish");
        public static Language Any => new Language(-1, "Any");
        public static Language Original => new Language(-2, "Original");

        public static List<Language> All
        {
            get
            {
                return new List<Language>
                {
                    Unknown,
                    English,
                    French,
                    Spanish,
                    German,
                    Italian,
                    Danish,
                    Dutch,
                    Japanese,
                    Icelandic,
                    Chinese,
                    Russian,
                    Polish,
                    Vietnamese,
                    Swedish,
                    Norwegian,
                    Finnish,
                    Turkish,
                    Portuguese,
                    Flemish,
                    Greek,
                    Korean,
                    Hungarian,
                    Hebrew,
                    Lithuanian,
                    Czech,
                    Romanian,
                    Hindi,
                    Thai,
                    Bulgarian,
                    PortugueseBR,
                    Arabic,
                    Ukrainian,
                    Persian,
                    Bengali,
                    Slovak,
                    Latvian,
                    SpanishLatino,
                    Catalan,
                    Croatian,
                    Serbian,
                    Bosnian,
                    Estonian,
                    Tamil,
                    Indonesian,
                    Telugu,
                    Macedonian,
                    Slovenian,
                    Malayalam,
                    Kannada,
                    Albanian,
                    Afrikaans,
                    Marathi,
                    Tagalog,
                    Urdu,
                    Romansh,
                    Mongolian,
                    Georgian,
                    Azerbaijani,
                    Uzbek,
                    Malay,
                    Irish,
                    Any,
                    Original
                };
            }
        }

        private static readonly Dictionary<int, Language> Lookup = All.ToDictionary(v => v.Id);

        public static Language FindById(int id)
        {
            if (id == 0)
            {
                return Unknown;
            }

            if (!Lookup.TryGetValue(id, out var language))
            {
                throw new ArgumentException("ID does not match a known language", nameof(id));
            }

            return language;
        }

        public static explicit operator Language(int id)
        {
            return FindById(id);
        }

        public static explicit operator int(Language language)
        {
            return language.Id;
        }

        public static explicit operator Language(string lang)
        {
            var language = All.FirstOrDefault(v => v.Name.Equals(lang, StringComparison.InvariantCultureIgnoreCase));

            if (language == null)
            {
                throw new ArgumentException("Language does not match a known language", nameof(lang));
            }

            return language;
        }

        public bool IsValid(bool throwOnMissing = true)
        {
            return Lookup.ContainsKey(Id) switch
            {
                false when throwOnMissing => throw new InvalidOperationException("ID does not match a known language"),
                false => false,
                _ => true
            };
        }
    }
}
