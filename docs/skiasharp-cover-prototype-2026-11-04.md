# SkiaSharp cover prototype — 2026-11-04

Status: isolated prototype based on `origin/develop` at `403cdd639`. It has not
been merged, deployed, or used for a production image. The follow-up below
adds a native runtime CI gate and an explicit animation fallback; CI results
are required before treating this as release ready.

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

## Follow-up: animation behavior

The compatibility decision is to serve the **original animated PNG or WebP**
when a same-format resized derivative is requested. SkiaSharp cannot encode
multiple frames through this path; decoding and re-encoding would lose frame
timing, partial-frame composition, and transparency. The resizer throws before
creating output, deletes any stale derivative, and the cover controller now
falls back to the full-size original for `.jpg`, `.png`, and `.webp` requests.
It also accepts `.webp` routes. This keeps the animation intact at the cost of
serving the full-size file. Animated input resized to JPEG remains a still
first frame, matching prior behavior.

The APNG check reads `acTL` before `IDAT` because Skia may report only one
frame for APNG. A 314-byte fixture contains three frames with 80, 120, and
200 ms delays, partial 13 × 7 and 14 × 7 later frames, and transparent pixels.
The focused tests confirm PNG rejection and cleanup, JPEG first-frame output,
WebP rejection, and same-extension HTTP fallback. The source bytes are served
unchanged, so frame timing and blend/dispose operations are preserved by the
fallback rather than reconstructed.

## Follow-up: runtime and appearance gates

The PR workflow now builds and **loads** the completed Alpine image on native
amd64 and arm64 GitHub runners, starts its server, and runs a dedicated smoke
target derived from that image. The smoke program performs 270 mixed
JPEG/PNG/WebP cover resizes in one process, checking format and dimensions
after each. This catches the observed arm64 failure, which happened on resize
8 or 9 under QEMU 8.2.2. All three prior QEMU traces show a .NET stack
overflow during a SkiaSharp handle lookup after the initial resizes; each
isolated resize had passed. That evidence does not distinguish an emulation
fault from a native arm64 fault. The native runner result is decisive for this
gate. The workflow also tests native loading on Windows and macOS, scans the
completed Alpine image, and compares a gzip-compressed image archive with the
same base commit's image on amd64. CI must complete before these claims can
be reported as passed.

On local Linux x64, the smoke program completed 270 resizes in 4.2 s with a
65 MiB peak working set. A disposable ImageSharp 3.1.12 baseline ran the same
270 operations on identical generated covers in 7.5 s with a 92 MiB peak.
Both figures include process startup and differ by runtime/library; they are
indicative, not a controlled throughput benchmark. The sample roles are a
1000 × 1500 movie poster (JPEG), 1600 × 900 TV fanart (WebP), and 900 × 900
transparent music logo (PNG). Resized heights are 250, 360, and 170.
The self-contained musl x64 smoke executable also ran against the packaged
application files inside the Alpine 3.22 rootfs using `bwrap`: 270 resizes in
5.2 s and a 63 MiB peak. The full local `tools/verify.sh` gate passed a clean
solution build, 228 API tests, 100 Host tests (5 skipped), and 10,961 Core
tests (53 skipped).

| Cover/output | Dimensions | ImageSharp bytes | SkiaSharp bytes | Difference |
| --- | ---: | ---: | ---: | ---: |
| Movie poster JPEG | 167 × 250 | 9,752 | 8,838 | −9.4% |
| Movie poster PNG | 167 × 250 | 26,638 | 24,659 | −7.4% |
| Movie poster WebP | 167 × 250 | 3,536 | 3,500 | −1.0% |
| TV fanart JPEG | 640 × 360 | 47,231 | 43,818 | −7.2% |
| TV fanart PNG | 640 × 360 | 167,519 | 132,400 | −21.0% |
| TV fanart WebP | 640 × 360 | 15,900 | 15,128 | −4.9% |
| Music logo JPEG | 170 × 170 | 7,948 | 6,430 | −19.1% |
| Music logo PNG | 170 × 170 | 25,660 | 21,011 | −18.1% |
| Music logo WebP | 170 × 170 | 14,640 | 4,994 | −65.9% |

The outputs decode at their expected dimensions. The PNG and WebP music logo
outputs retain an alpha range of 0–255; JPEG is opaque. Source EXIF and ICC
removal is covered by the existing tests. The synthetic music logo's WebP
looks visibly different: ImageSharp paints a green background while Skia
retains transparency. JPEG/PNG edges also differ because the samplers differ,
as quantified above. The image-size table measures derivative files; the
completed compressed container-image impact remains a CI gate.
