# Quality and language catalogs (porting step 8d)

Status: **proposed**, waiting for the owner's answers to the open choices
below. No code is written against this document yet.

Decided earlier (2026-10-20): Plattarr owns global quality and language
catalogs with fresh Plattarr numbering. Every importer and every
compatibility API translates IDs, Radarr's included. Quality profiles and
custom formats are global, with optional per-media-type scoping.

## What upstream stores

Radarr, Sonarr, and Lidarr each keep qualities and languages as static
classes in code (`Quality`, `Language`) and store only the integer ID. In
Plattarr the IDs reach the database through two converters,
`QualityIntConverter` and `LanguageIntConverter`, registered in
`TableMapping`. Every stored ID passes through them:

| Where | What |
|---|---|
| `QualityDefinitions.Quality` | Quality ID, one row per quality |
| `QualityProfiles.Items`, `Cutoff` | Quality IDs; quality groups use IDs from 1000 up |
| `QualityModel` JSON in `History`, `Blocklist`, `Movies_MovieFiles` | Quality ID |
| `PendingReleases.ParsedMovieInfo` | Quality ID and language IDs |
| `Languages` JSON in `History`, `Blocklist`, `Movies_MovieFiles` | Language IDs |
| `QualityProfiles.Language`, `Movies_MovieMetadata.OriginalLanguage`, translations, subtitle files | Language IDs |
| `CustomFormats.Specifications` (`LanguageSpecification`) | Language ID |
| `IndexerOverrides.MultiLanguages` | Language IDs |
| `Config` key `UILanguage` | Language ID |

Because every write goes through the converters, a fresh database written by
code whose `Quality` and `Language` objects carry catalog IDs holds catalog
IDs everywhere, with no per-table work.

## How the three apps disagree

Qualities:

- Radarr has 30 video qualities plus `Unknown`; Sonarr has 21 plus `Unknown`, all of which exist in
  Radarr under the same name except the two remuxes (Sonarr
  `Bluray-1080p Remux` is Radarr `Remux-1080p`).
- The same quality has different IDs. Radarr `Bluray-480p` is 20, Sonarr's
  is 13. Radarr 20 to 22 are `Bluray-480p`, `Bluray-576p`, `BR-DISK`;
  Sonarr 20 to 22 are the two remuxes and `Bluray-576p`.
- Lidarr's 38 qualities are audio formats (`MP3-320`, `FLAC`) on IDs 0 to
  37, which overlap every video ID.
- Default weights differ (Sonarr ranks `Raw-HD` between HDTV-1080p and
  WEB-720p; Radarr ranks it last).

Languages:

- 0 `Unknown`, -1 `Any` (Radarr only), and -2 `Original` mean the same thing
  wherever they exist.
- IDs 1 to 25 agree in all three apps. From 26 up, Radarr and Sonarr
  diverge (Radarr 26 is Hindi, Sonarr 26 is Arabic). Lidarr's 34 languages
  match Radarr's IDs exactly.
- Radarr has 60 entries, Sonarr 55. Sonarr-only: Azerbaijani, Irish, Malay,
  Uzbek. Radarr-only: Afrikaans, Albanian, Bengali, Kannada, Marathi,
  Mongolian, Tagalog, Telugu (and the `Any` sentinel).

Custom format vocabulary:

- Radarr's `SourceSpecification` uses `QualitySource` (`CAM`, `TELESYNC`,
  `TELECINE`, `WORKPRINT`, `DVD`, `TV`, `WEBDL`, `WEBRIP`, `BLURAY`) plus a
  separate `QualityModifierSpecification` (`REGIONAL`, `SCREENER`, `RAWHD`,
  `BRDISK`, `REMUX`).
- Sonarr folds two modifiers into its source enum (`TelevisionRaw`,
  `BlurayRaw`) and has no modifier specification. Every Sonarr source maps
  to one Radarr source plus modifier, so Radarr's vocabulary is the global
  one and Sonarr's is translated.

## Proposal

### Catalogs stay in code

`Quality` and `Language` remain static classes, as upstream. A database
catalog table would add nothing: the parsers that produce qualities are code,
and adding a quality needs a parser change anyway. The `QualityDefinitions`
table keeps the user-editable part (title, size limits) per catalog quality,
and `QualityDefinitionService` adds missing rows at startup as it does today.

### One quality catalog with a media domain

Each catalog quality carries a `Domain`: `Video` or `Audio`. Movies and TV
share the video qualities; Music uses the audio ones. A quality profile holds
qualities from one domain only, enforced by validation, and its `AppliesTo`
must name media types of that domain.

- Video: the union of Radarr's and Sonarr's qualities, 30 entries, keeping
  Radarr's source, resolution, and modifier attributes. The two remuxes keep
  Radarr's names in the UI.
- Audio: Lidarr's 37 audio qualities (Lidarr `Unknown` folds into the shared
  `Unknown`).
- `Unknown` stays 0 and belongs to both domains.

### Numbering (choice 1)

Recommended: ranges by domain, numbered in default weight order at birth,
never reused.

- 0: `Unknown`
- 1 to 199: video, in Radarr's default weight order (`WORKPRINT` 1 ...
  `Raw-HD` 30)
- 201 to 399: audio, in Lidarr's default weight order
- 1000 and up: quality groups inside profiles, as today

A new quality takes the next free ID in its range. Order on screen comes
from weights and profiles, never from IDs.

### Languages (choice 2)

Recommended: keep the three sentinels unchanged (0 `Unknown`, -1 `Any`,
-2 `Original`) because they mean the same in every app and the frontend and
custom format code test for them. Number the 61 real languages (Radarr's 57 plus Sonarr's 4) from 1 in
alphabetical order of their English names; later additions append.

### Translation tables

`Qualities/Upstream/RadarrQualityIds`, `SonarrQualityIds`, `LidarrQualityIds`
and the same three for languages: static two-way maps between an upstream ID
and a catalog ID, with a test that every upstream ID maps and every catalog
entry of the right domain maps back. Users:

- the migration importer (Phase 2), for every stored ID in the tables above;
- the compatibility APIs (`/radarr/api/v3` first), on every resource field
  that carries a quality or language ID, in both directions;
- Sonarr's `QualitySource` to Radarr source plus modifier, for custom
  format specifications.

`/api/v3`, which the current UI uses, carries catalog IDs. The frontend
round-trips IDs it receives from the API; a search found no hard-coded
quality or language ID other than 0.

### Profiles and custom formats

`QualityProfiles` and `CustomFormats` gain `AppliesTo`: a JSON list of
`MediaKind`. Empty means every media type of the profile's domain. Movies
read only profiles and custom formats that apply to Movies. A profile in use
by a movie can't drop Movies from `AppliesTo`.

### Order of work

1. 8d1: catalogs and translation tables. `Quality` and `Language` carry
   catalog IDs; Radarr's parsers keep producing the same objects, so they
   output catalog IDs with no parser change. Fix tests that hard-code
   upstream IDs. `001_initial` seeds no `QualityDefinitions` rows;
   `QualityDefinitionService` creates them at startup from the catalog.
2. 8d2: `AppliesTo` on profiles and custom formats, the domain check, and
   filtered reads in the movie code.

## Open choices for the owner

1. Quality numbering: domain ranges (recommended) or one contiguous
   sequence.
2. Language numbering: keep the sentinels and number the rest
   alphabetically (recommended), or renumber the sentinels too.
3. Audio qualities: in the one catalog with a `Domain` (recommended), or a
   separate audio catalog type.
