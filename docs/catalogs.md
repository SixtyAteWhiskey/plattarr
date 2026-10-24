# Quality and language catalogs (built in porting step 8d)

Status: **decided** 2026-10-20 (session 13). The owner chose a separate
audio catalog and left numbering to Claude; the choices are recorded in
"Decisions" at the end.

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

- Radarr has 29 video qualities plus `Unknown`; Sonarr has 21 plus `Unknown`, all of which exist in
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

### Two quality catalogs: video and audio

Movies and TV share one video catalog, the existing `Quality` class. Music
gets its own audio catalog type when the Music module arrives (Phase 4), so
video and audio qualities never mix in one profile, one parser, or one API
resource. Quality profiles follow the catalogs: `QualityProfiles` holds video
profiles; Music brings its own profile table with Lidarr's shape.

- Video: the union of Radarr's and Sonarr's qualities, 29 entries, keeping
  Radarr's source, resolution, and modifier attributes. The two remuxes keep
  Radarr's names in the UI.
- Audio (Phase 4): Lidarr's 37 audio qualities.
- `Unknown` is 0 in both catalogs.

### Numbering

The two catalogs use disjoint ranges, so a quality ID found in a shared table
(`History.Quality`, `Blocklist.Quality`) can't be read as the wrong kind of
quality. IDs are numbered in default weight order at birth and never reused.

- 0: `Unknown`
- 1 to 199: video, in Radarr's default weight order (`WORKPRINT` 1 ...
  `Raw-HD` 29)
- 201 to 399: audio (Phase 4), in Lidarr's default weight order
- 1000 and up: quality groups inside profiles, as today

A new quality takes the next free ID in its range. Order on screen comes
from weights and profiles, never from IDs.

### Languages

Keep the three sentinels unchanged (0 `Unknown`, -1 `Any`,
-2 `Original`) because they mean the same in every app and the frontend and
custom format code test for them. Number the 61 real languages (Radarr's 57 plus Sonarr's 4) from 1 in
alphabetical order of their English names; later additions append.

As built (8d1): Azerbaijani, Irish, Malay, and Uzbek, which only Sonarr had,
are in the catalog and in `IsoLanguages`. Radarr's title parser doesn't
detect them; the TV module brings Sonarr's rules for them.

### Translation tables

`Qualities/Upstream/RadarrQualityIds`, `SonarrQualityIds`, `LidarrQualityIds`
and the same for languages: static two-way maps between an upstream ID
and a catalog ID, with a test that every upstream ID maps and every catalog
entry maps back. `LidarrQualityIds` comes with the audio catalog. Users:

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
`MediaKind`. Empty means every media type the profile's catalog serves
(Movies and TV for video profiles). Movies
read only profiles and custom formats that apply to Movies. A profile in use
by a movie can't drop Movies from `AppliesTo`.

As built (8d2): both tables store `AppliesTo` as a JSON list of media type
names (`["tv"]`), default `[]`. Movie custom format scoring skips formats
that don't apply to Movies. `QualityProfileExistsValidator`, used wherever a
profile is assigned to a movie, import list, or collection, rejects a profile
that doesn't apply to Movies. The v3 resources carry `appliesTo`; upstream
Radarr clients omit it, so a missing value on update keeps the stored one. A
quality profile accepts only `movies` and `tv`. `/api/v3` still lists every
profile and format, since the UI uses it; the `/radarr/api/v3`
compatibility API will list only the ones that apply to Movies.

As built (Music step 0): `AudioQuality`, `AudioQualityDefinition` and `AudioQualityModel` are in
`NzbDrone.Core.Qualities`, with `LidarrQualityIds` beside the other two maps. `Unknown` is 0 and the
37 audio qualities are 201 to 237. `module-interface.md` ("Music step 0: as built") has the details.

## Decisions

| Choice | Decision | Reason |
|---|---|---|
| Audio qualities | Separate audio catalog (owner) | Owner's choice. Lidarr's qualities and profiles already differ in shape from the video ones. |
| Quality numbering | Disjoint ranges: video 1 to 199, audio 201 to 399, groups 1000 and up (Claude, delegated) | Shared activity tables store either kind; disjoint ranges keep an ID unambiguous. |
| Language numbering | Sentinels 0, -1, -2 unchanged; real languages from 1 alphabetically (Claude, delegated) | The sentinels mean the same in every app and code tests for them; renumbering them adds translation with no benefit. |
