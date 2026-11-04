# SkiaSharp cover prototype — 2026-11-04

Status: isolated prototype based on `origin/develop` at `403cdd639`. It has not
been merged, deployed, or used for a production image.

## Scope

The shared `ImageResizer` now uses SkiaSharp 4.154.0. Movie, TV, and Music cover
services retain their calls to that shared interface and their existing
concurrency limit. Input is detected from the bytes and limited to JPEG, PNG,
and WebP. Output follows the destination extension (`.jpg`, `.jpeg`, `.png`, or
`.webp`). Resizing preserves the aspect ratio and sets the requested height.
JPEG quality remains 92; WebP uses its prior default quality of 75. PNG and
transparent pixels are retained. Decoding and re-encoding remove EXIF and ICC
metadata. Failure still deletes a destination file, including a stale one.

The new sampler is Skia's linear filter with linear mipmaps. ImageSharp's
simple `Resize` overload uses bicubic, so pixel values and edge appearance are
not identical. A disposable comparison of a 1200 × 800 textured JPEG at
540 × 360 found these decoded-output differences:

| Output | ImageSharp bytes | SkiaSharp bytes | Mean absolute channel difference (0–255) | PSNR between outputs |
| --- | ---: | ---: | ---: | ---: |
| JPEG | 36,556 | 35,952 | 1.05 | 43.10 dB |
| PNG | 237,655 | 216,929 | 0.70 | 45.87 dB |
| WebP | 6,300 | 6,298 | 1.07 | 41.55 dB |

The repository's transparent logo was also checked visually in JPEG and PNG.
Its PNG output retains alpha. An EXIF-rotated JPEG kept the same pixel
dimensions and first-pixel orientation as the prior output, with no orientation
tag in either result. A JPEG with an ICC source profile produced output without
an ICC profile in both implementations.

## Validation

- `tools/verify.sh` with the ImageResizer and three media cover service
  fixtures passed a nonincremental Release solution build, 228 API tests, 100
  Host tests (5 skipped), and 43 selected Core tests. The ten focused resizer
  cases cover dimensions, formats, WebP input, transparency, metadata, and
  cleanup.
- The Dockerfile's backend command, `./build.sh --backend -f net10.0 -r RID`,
  completed for both `linux-musl-x64` and `linux-musl-arm64` with zero errors.
  MSBuild retried parallel copies of shared runtime files (MSB3026 warnings)
  and completed. Both publish directories contain an architecture-matching
  `libSkiaSharp.so` and a musl .NET 10 application host.
- `./build.sh --packages` also completed for both RIDs. It used an existing
  prebuilt UI solely to exercise packaging, and both packages contain the
  native library, SkiaSharp license, and third-party notices. The frontend
  build itself was outside this backend-only prototype.
- An Alpine 3.22.7 root filesystem with the Dockerfile's exact `apk` runtime
  package list loaded SkiaSharp successfully. The x64 smoke process decoded
  PNG, JPEG, and WebP inputs and wrote all three output formats at height 170.
  On arm64 under QEMU 8.2.2, each of the nine input/output pairs passed in a
  fresh process. Running all nine in one arm64 process repeatedly ended in a
  .NET stack overflow after or near the final resize. The cause is unresolved;
  native arm64 hardware testing is required.
- `dotnet list ... package --vulnerable --include-transitive` found no known
  vulnerable package in the Console project's current NuGet sources. The six
  ImageSharp audit suppressions were removed with the ImageSharp reference.
  This is a NuGet audit, not a scan of a completed container image or all
  native Skia code.

No Docker or Podman executable is installed on the local host, so an actual
multi-architecture Docker image build, image scan, and startup test remain to
be run. The backend and Alpine rootfs checks above exercise the target RIDs
and native loading but do not replace those gates.

## Packaging and license

SkiaSharp is [MIT licensed](https://github.com/mono/SkiaSharp/blob/main/LICENSE.md).
The Linux `NoDependencies` package includes musl x64 and arm64 assets and is
the [upstream recommendation for Alpine containers](https://github.com/mono/SkiaSharp/blob/main/documentation/dev/packages.md).
The package's original MIT license and third-party notices are copied into
`distribution/licenses` and included by `build.sh` in packaged application
output. The native library requires musl and `libstdc++`; Alpine's existing
`icu-libs` package brings `libstdc++` on both tested architectures. The x64
native library is 12,358,608 bytes, versus 2,100,736 bytes for the removed
ImageSharp managed DLL. Measure the final compressed image size before a merge.

## Remaining behavior gap

ImageSharp preserves multiple frames when an animated WebP is resized to WebP
and when it saves an animated PNG. SkiaSharp's normal bitmap decode and image
encode produce one frame. The prototype detects multi-frame input and refuses
PNG or WebP output so the cover service can fall back to the original image
instead of silently freezing the animation. Animated input to JPEG still uses
the first frame, as the prior JPEG output did. Animated PNG/WebP resizing is
therefore a known compatibility gap and should be resolved or explicitly
accepted before merging.

The arm64 repeated-resize crash under QEMU, native arm64 hardware run, actual
Docker image build and scan, and Windows/macOS native runtime checks remain
open. This branch is an evaluation, not a release-ready replacement.
