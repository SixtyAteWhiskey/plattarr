using System;
using System.Diagnostics;
using System.IO;
using NzbDrone.Core.MediaCover;
using SkiaSharp;

var folder = Path.Combine(Path.GetTempPath(), "plattarr-cover-smoke");
Directory.CreateDirectory(folder);
var cases = new[]
{
    (Name: "movie-poster", Width: 1000, Height: 1500, Source: SKEncodedImageFormat.Jpeg, TargetHeight: 250),
    (Name: "tv-fanart", Width: 1600, Height: 900, Source: SKEncodedImageFormat.Webp, TargetHeight: 360),
    (Name: "music-logo", Width: 900, Height: 900, Source: SKEncodedImageFormat.Png, TargetHeight: 170)
};
var outputs = new[]
{
    (Extension: "jpg", Format: SKEncodedImageFormat.Jpeg),
    (Extension: "png", Format: SKEncodedImageFormat.Png),
    (Extension: "webp", Format: SKEncodedImageFormat.Webp)
};

foreach (var item in cases)
{
    using var bitmap = new SKBitmap(item.Width, item.Height);
    bitmap.Erase(item.Name == "music-logo" ? SKColors.Transparent : SKColors.DarkSlateBlue);
    using var canvas = new SKCanvas(bitmap);
    using var paint = new SKPaint { IsAntialias = true };

    for (var i = 0; i < 64; i++)
    {
        paint.Color = new SKColor((byte)(i * 47), (byte)(i * 83), (byte)(i * 29), (byte)(item.Name == "music-logo" ? 160 : 255));
        canvas.DrawCircle(i * item.Width / 64f, i * item.Height / 64f, item.Width / 9f, paint);
    }

    using var image = SKImage.FromBitmap(bitmap);
    using var encoded = image.Encode(item.Source, 85);
    File.WriteAllBytes(Path.Combine(folder, item.Name + "." + item.Source.ToString().ToLowerInvariant()), encoded.ToArray());
}

var timer = Stopwatch.StartNew();
var resizer = new ImageResizer(null);
var count = 0;
for (var round = 0; round < 30; round++)
{
    foreach (var item in cases)
    {
        foreach (var output in outputs)
        {
            var source = Path.Combine(folder, item.Name + "." + item.Source.ToString().ToLowerInvariant());
            var destination = Path.Combine(folder, item.Name + "-small." + output.Extension);
            resizer.Resize(source, destination, item.TargetHeight);
            using var stream = File.OpenRead(destination);
            using var codec = SKCodec.Create(stream) ?? throw new Exception("Output could not be decoded: " + destination);
            var expectedWidth = Math.Max(1, (int)Math.Round((double)item.Width * item.TargetHeight / item.Height));

            if (codec.EncodedFormat != output.Format || codec.Info.Width != expectedWidth || codec.Info.Height != item.TargetHeight)
            {
                throw new Exception($"Unexpected output: {destination}: {codec.EncodedFormat} {codec.Info.Width}x{codec.Info.Height}");
            }

            count++;
        }
    }
}

timer.Stop();
Console.WriteLine($"PASS {count} mixed cover resizes in one process; {timer.Elapsed.TotalSeconds:F1}s; peak working set {Process.GetCurrentProcess().PeakWorkingSet64 / 1024 / 1024} MiB");
