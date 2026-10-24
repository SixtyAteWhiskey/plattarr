import MediaKind from 'typings/MediaKind';

// Mirrors ModuleCategoryFieldNames.Tag (src/NzbDrone.Core/Download/Clients/ModuleCategoryFieldNames.cs):
// the download client form names its category field after the media type, the way the upstream
// apps do (movieCategory, tvCategory, musicCategory). A new media type needs an entry here, and
// the compiler enforces it through the Record type.
const CATEGORY_FIELD_NAMES: Record<MediaKind, string> = {
  movies: 'movieCategory',
  tv: 'tvCategory',
  music: 'musicCategory',
};

export interface ProviderFieldValue {
  name: string;
  value?: unknown;
}

export function getCategoryFieldName(kind: MediaKind) {
  return CATEGORY_FIELD_NAMES[kind];
}

// The category a download client routes this media type's downloads to, or null when the client
// has no category field for it (a client that does not route by category) or the field is empty.
export function getClientCategory(
  fields: ProviderFieldValue[] | undefined,
  kind: MediaKind
) {
  const name = getCategoryFieldName(kind);
  const value = fields?.find((field) => field.name === name)?.value;

  return typeof value === 'string' && value.trim() !== '' ? value : null;
}
