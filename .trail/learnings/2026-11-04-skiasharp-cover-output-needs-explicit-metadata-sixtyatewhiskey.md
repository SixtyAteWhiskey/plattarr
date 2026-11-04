---
title: SkiaSharp cover output needs explicit metadata and animation handling
author: SixtyAteWhiskey
date: 2026-11-04
branch: codex/skiasharp-cover-prototype
id: l-3d3acabd
scope: repo
status: active
src: agent
touches:
  - build.sh
  - src/Directory.Build.props
  - src/NzbDrone.Console/Radarr.Console.csproj
  - src/NzbDrone.Core.Test/MediaCoverTests/ImageResizerFixture.cs
  - src/NzbDrone.Core.Test/Radarr.Core.Test.csproj
  - src/NzbDrone.Core/MediaCover/ImageResizer.cs
  - src/NzbDrone.Core/Movies/Covers/MediaCoverService.cs
  - src/NzbDrone.Core/Music/Covers/MediaCoverService.cs
  - src/NzbDrone.Core/Radarr.Core.csproj
  - src/NzbDrone.Core/Tv/Covers/MediaCoverService.cs
---

## Decided
Use a resized SKImageInfo without a color space. SkiaSharp otherwise embeds a new sRGB ICC profile even when the input has none; the prior ImageSharp path skipped metadata. Detect multi-frame sources before PNG or WebP output and let the cover service fall back to the original instead of silently freezing an animation.

## Tried and ruled out
Direct SKBitmap.Decode, Resize(SKSizeI), and SKImage.Encode produced valid static covers but added an ICC profile and flattened animated WebP from two frames to one. Keeping that simple pipeline would change existing cover behavior. A QEMU 8.2.2 arm64 smoke passed each format pair in a fresh process but stack-overflowed near the end of repeated resizes; it cannot settle native arm64 runtime safety.

## Watch out
The prototype still cannot resize animated PNG/WebP output while keeping frames. Before merging, resolve or explicitly accept that gap and run repeated cover resizing on native arm64 Alpine hardware, plus a real image build and scan.

In the code: src/NzbDrone.Core/MediaCover/ImageResizer.cs; src/NzbDrone.Core.Test/MediaCoverTests/ImageResizerFixture.cs; docs/skiasharp-cover-prototype-2026-11-04.md. Current baseline uses ImageSharp 3.1.12; prototype uses SkiaSharp 4.154.0.
