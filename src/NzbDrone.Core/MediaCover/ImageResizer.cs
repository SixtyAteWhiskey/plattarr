using System;
using System.IO;
using NzbDrone.Common.Disk;
using SkiaSharp;

namespace NzbDrone.Core.MediaCover
{
    public interface IImageResizer
    {
        void Resize(string source, string destination, int height);
    }

    public class ImageResizer : IImageResizer
    {
        private readonly IDiskProvider _diskProvider;
        public ImageResizer(IDiskProvider diskProvider)
        {
            _diskProvider = diskProvider;
        }

        public void Resize(string source, string destination, int height)
        {
            try
            {
                if (height <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(height));
                }

                var format = Path.GetExtension(destination).ToLowerInvariant() switch
                {
                    ".jpg" or ".jpeg" => SKEncodedImageFormat.Jpeg,
                    ".png" => SKEncodedImageFormat.Png,
                    ".webp" => SKEncodedImageFormat.Webp,
                    _ => throw new NotSupportedException("The media cover extension must be JPEG, PNG, or WebP.")
                };

                using var input = File.OpenRead(source);
                using var codec = SKCodec.Create(input) ?? throw new InvalidDataException("The media cover could not be decoded.");

                if (codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp))
                {
                    throw new NotSupportedException("The media cover must be JPEG, PNG, or WebP.");
                }

                if (codec.FrameCount > 1 && format != SKEncodedImageFormat.Jpeg)
                {
                    throw new NotSupportedException("Animated PNG and WebP covers cannot be resized without losing frames.");
                }

                using var bitmap = SKBitmap.Decode(codec) ?? throw new InvalidDataException("The media cover could not be decoded.");
                var width = Math.Max(1, (int)Math.Round((double)bitmap.Width * height / bitmap.Height));

                // The old decoder skipped metadata, including the source color profile.
                using var resized = bitmap.Resize(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
                    ?? throw new InvalidDataException("The media cover could not be resized.");
                using var image = SKImage.FromBitmap(resized);
                using var encoded = image.Encode(format, format == SKEncodedImageFormat.Webp ? 75 : 92)
                    ?? throw new InvalidDataException("The media cover could not be encoded.");
                using var output = File.Create(destination);
                encoded.SaveTo(output);
            }
            catch
            {
                if (_diskProvider.FileExists(destination))
                {
                    _diskProvider.DeleteFile(destination);
                }

                throw;
            }
        }
    }
}
