using System.Collections.Immutable;

namespace NzbDrone.Core.Parser
{
    // Makes a release or media name safe to use as a file name. Download clients use it for the
    // names they hand to a client or a watch folder, and the movie file name builder uses it for
    // every token it writes, so both clean names the same way.
    public static class FileNameCleaner
    {
        public static readonly ImmutableArray<string> BadCharacters = ImmutableArray.Create("\\", "/", "<", ">", "?", "*", "|", "\"");
        public static readonly ImmutableArray<string> GoodCharacters = ImmutableArray.Create("+", "+", "", "", "!", "-", "", "");

        // Smart colon handling (": " becomes " - ", ":" becomes "-") with illegal characters replaced.
        public static string Clean(string name)
        {
            return Clean(name, true, null);
        }

        // colonReplacement null means Smart. It is ignored when replaceIllegalCharacters is false:
        // colons and the characters in BadCharacters are then removed.
        public static string Clean(string name, bool replaceIllegalCharacters, string colonReplacement)
        {
            var result = name;

            if (replaceIllegalCharacters)
            {
                if (colonReplacement == null)
                {
                    // Smart replaces a colon followed by a space with space dash space for a better appearance
                    result = result.Replace(": ", " - ");
                    result = result.Replace(":", "-");
                }
                else
                {
                    result = result.Replace(":", colonReplacement);
                }
            }
            else
            {
                result = result.Replace(":", string.Empty);
            }

            for (var i = 0; i < BadCharacters.Length; i++)
            {
                result = result.Replace(BadCharacters[i], replaceIllegalCharacters ? GoodCharacters[i] : string.Empty);
            }

            return result.TrimStart(' ', '.').TrimEnd(' ');
        }
    }
}
