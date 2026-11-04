using System;
using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.Test.Framework;
using SkiaSharp;

namespace NzbDrone.Core.Test.MediaCoverTests
{
    [TestFixture]
    public class ImageResizerFixture : CoreTest<ImageResizer>
    {
        [SetUp]
        public void SetUp()
        {
            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.FileExists(It.IsAny<string>()))
                  .Returns<string>(s => File.Exists(s));

            Mocker.GetMock<IDiskProvider>()
                  .Setup(v => v.DeleteFile(It.IsAny<string>()))
                  .Callback<string>(s => File.Delete(s));
        }

        [Test]
        public void should_resize_image()
        {
            var mainFile = Path.Combine(TempFolder, "logo.png");
            var resizedFile = Path.Combine(TempFolder, "logo-170.png");

            File.Copy(GetTestPath("Files/1024.png"), mainFile);

            Subject.Resize(mainFile, resizedFile, 170);

            var fileInfo = new FileInfo(resizedFile);
            fileInfo.Exists.Should().BeTrue();
            fileInfo.Length.Should().BeInRange(1000, 30000);

            using (var image = SKBitmap.Decode(resizedFile))
            {
                image.Height.Should().Be(170);
                image.Width.Should().Be(170);
                image.GetPixel(0, 0).Alpha.Should().Be(0);
                image.GetPixel(85, 85).Alpha.Should().Be(255);
            }
        }

        [TestCase("jpg", SKEncodedImageFormat.Jpeg)]
        [TestCase("png", SKEncodedImageFormat.Png)]
        [TestCase("webp", SKEncodedImageFormat.Webp)]
        public void should_preserve_aspect_ratio_and_encode_for_destination_extension(string extension, SKEncodedImageFormat format)
        {
            var source = Path.Combine(TempFolder, "source.png");
            var destination = Path.Combine(TempFolder, "cover." + extension);

            using (var bitmap = new SKBitmap(400, 200))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                using var output = File.Create(source);
                encoded.SaveTo(output);
            }

            Subject.Resize(source, destination, 100);

            using var stream = File.OpenRead(destination);
            using var codec = SKCodec.Create(stream);
            codec.EncodedFormat.Should().Be(format);
            codec.Info.Width.Should().Be(200);
            codec.Info.Height.Should().Be(100);
        }

        [Test]
        public void should_decode_webp_source_and_write_jpeg()
        {
            var source = Path.Combine(TempFolder, "source.webp");
            var destination = Path.Combine(TempFolder, "cover.jpg");

            using (var bitmap = new SKBitmap(400, 200))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Webp, 75);
                using var output = File.Create(source);
                encoded.SaveTo(output);
            }

            Subject.Resize(source, destination, 100);

            using var imageResult = SKBitmap.Decode(destination);
            imageResult.Width.Should().Be(200);
            imageResult.Height.Should().Be(100);
        }

        [Test]
        public void should_strip_source_metadata()
        {
            var source = Path.Combine(TempFolder, "source.jpg");
            var destination = Path.Combine(TempFolder, "cover.jpg");
            var marker = "private-cover-metadata";

            using (var bitmap = new SKBitmap(400, 200))
            {
                bitmap.Erase(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 92);
                var jpeg = encoded.ToArray();
                var exif = Encoding.ASCII.GetBytes("Exif\0\0II*\0\x08\0\0\0\0\0\0\0\0\0\0" + marker);
                var length = exif.Length + 2;

                using var output = File.Create(source);
                output.Write(jpeg, 0, 2);
                output.WriteByte(0xFF);
                output.WriteByte(0xE1);
                output.WriteByte((byte)(length >> 8));
                output.WriteByte((byte)length);
                output.Write(exif);
                output.Write(jpeg, 2, jpeg.Length - 2);
            }

            Subject.Resize(source, destination, 100);

            Encoding.ASCII.GetString(File.ReadAllBytes(source)).Should().Contain(marker);
            Encoding.ASCII.GetString(File.ReadAllBytes(destination)).Should().NotContain(marker);
            Encoding.ASCII.GetString(File.ReadAllBytes(destination)).Should().NotContain("ICC_PROFILE");
        }

        [Test]
        public void should_delete_file_if_failed()
        {
            var mainFile = Path.Combine(TempFolder, "junk.png");
            var resizedFile = Path.Combine(TempFolder, "junk-170.png");

            File.WriteAllText(mainFile, "Just some junk data that should make it throw an Exception.");
            File.WriteAllText(resizedFile, "stale cover");

            Assert.Throws(Is.InstanceOf<Exception>(), () => Subject.Resize(mainFile, resizedFile, 170));

            File.Exists(resizedFile).Should().BeFalse();
        }

        [Test]
        public void should_reject_unsupported_output_and_remove_stale_file()
        {
            var source = GetTestPath("Files/1024.png");
            var destination = Path.Combine(TempFolder, "cover.gif");
            File.WriteAllText(destination, "stale cover");

            Assert.Throws<NotSupportedException>(() => Subject.Resize(source, destination, 170));

            File.Exists(destination).Should().BeFalse();
        }

        [Test]
        public void should_reject_unsupported_input_format()
        {
            var source = Path.Combine(TempFolder, "source.gif");
            var destination = Path.Combine(TempFolder, "cover.png");
            File.WriteAllBytes(source, Convert.FromBase64String("R0lGODdhAQABAIEAAP///wAAAAAAAAAAACwAAAAAAQABAAAIBAABBAQAOw=="));

            Assert.Throws<NotSupportedException>(() => Subject.Resize(source, destination, 100));

            File.Exists(destination).Should().BeFalse();
        }

        [Test]
        public void should_not_silently_flatten_animated_webp()
        {
            var source = Path.Combine(TempFolder, "animated.webp");
            var destination = Path.Combine(TempFolder, "animated-small.webp");
            const string animation = "UklGRsIAAABXRUJQVlA4WAoAAAACAAAAAQAAAAAAQU5JTQYAAAAAAAAAAABBTk1GSAAAAAAAAAAAAAEAAAAAAGQAAAJWUDggMAAAADABAJ0BKgIAAQACADQloAADcAD+8MQL//5Bj/Q1/grwB///kB//+QH//5Af/x5AAEFOTUZGAAAAAAAAAAAAAQAAAAAAZAAAAFZQOCAuAAAANAEAnQEqAgABAAAANCWgAANwAP75k2///kB//+QH//5Af/yA//4i6e8AwQAAAA==";
            File.WriteAllBytes(source, Convert.FromBase64String(animation));

            Assert.Throws<NotSupportedException>(() => Subject.Resize(source, destination, 10));

            File.Exists(destination).Should().BeFalse();

            var jpegDestination = Path.Combine(TempFolder, "animated-small.jpg");
            Subject.Resize(source, jpegDestination, 10);

            using var jpegStream = File.OpenRead(jpegDestination);
            using var jpegCodec = SKCodec.Create(jpegStream);
            jpegCodec.EncodedFormat.Should().Be(SKEncodedImageFormat.Jpeg);
            jpegCodec.Info.Width.Should().Be(20);
            jpegCodec.Info.Height.Should().Be(10);
        }

        [Test]
        public void should_not_silently_flatten_animated_png_with_partial_frames()
        {
            var source = GetTestPath("Files/animated-partial.png");
            var destination = Path.Combine(TempFolder, "animated-small.png");
            File.WriteAllText(destination, "stale cover");

            Assert.Throws<NotSupportedException>(() => Subject.Resize(source, destination, 8));

            File.Exists(destination).Should().BeFalse();

            var jpegDestination = Path.Combine(TempFolder, "animated-small.jpg");
            Subject.Resize(source, jpegDestination, 8);

            using var jpegStream = File.OpenRead(jpegDestination);
            using var jpegCodec = SKCodec.Create(jpegStream);
            jpegCodec.EncodedFormat.Should().Be(SKEncodedImageFormat.Jpeg);
            jpegCodec.Info.Width.Should().Be(12);
            jpegCodec.Info.Height.Should().Be(8);
        }
    }
}
