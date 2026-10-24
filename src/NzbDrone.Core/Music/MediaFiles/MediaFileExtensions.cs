using System;
using System.Collections.Generic;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.Music.MediaFiles
{
    public static class MediaFileExtensions
    {
        private static Dictionary<string, AudioQuality> _fileExtensions;

        static MediaFileExtensions()
        {
            _fileExtensions = new Dictionary<string, AudioQuality>(StringComparer.OrdinalIgnoreCase)
            {
                { ".mp2", AudioQuality.Unknown },
                { ".mp3", AudioQuality.Unknown },
                { ".m4a", AudioQuality.Unknown },
                { ".m4b", AudioQuality.Unknown },
                { ".m4p", AudioQuality.Unknown },
                { ".ogg", AudioQuality.Unknown },
                { ".oga", AudioQuality.Unknown },
                { ".opus", AudioQuality.Unknown },
                { ".wma", AudioQuality.WMA },
                { ".wav", AudioQuality.WAV },
                { ".wv", AudioQuality.WAVPACK },
                { ".flac", AudioQuality.FLAC },
                { ".ape", AudioQuality.APE },
                { ".aif", AudioQuality.Unknown },
                { ".aiff", AudioQuality.Unknown },
                { ".aifc", AudioQuality.Unknown }
            };
        }

        public static HashSet<string> Extensions => new HashSet<string>(_fileExtensions.Keys, StringComparer.OrdinalIgnoreCase);

        public static AudioQuality GetQualityForExtension(string extension)
        {
            if (_fileExtensions.ContainsKey(extension))
            {
                return _fileExtensions[extension];
            }

            return AudioQuality.Unknown;
        }
    }
}
