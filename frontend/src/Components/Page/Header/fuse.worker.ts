import Fuse from 'fuse.js';
import { SuggestedMedia } from './MediaSearchInput';

const fuseOptions = {
  shouldSort: true,
  includeMatches: true,
  ignoreLocation: true,
  threshold: 0.3,
  minMatchCharLength: 1,
  keys: [
    'title',
    'originalTitle',
    'year',
    'alternateTitles.title',
    'tmdbId',
    'tvdbId',
    'imdbId',
    'tags.label',
  ],
};

function getSuggestions(media: SuggestedMedia[], value: string) {
  const limit = 10;
  let suggestions = [];

  if (value.length === 1) {
    for (let i = 0; i < media.length; i++) {
      const m = media[i];
      if (m.firstCharacter === value.toLowerCase()) {
        suggestions.push({
          item: media[i],
          indices: [[0, 0]],
          matches: [
            {
              value: m.title,
              key: 'title',
            },
          ],
          refIndex: 0,
        });
        if (suggestions.length > limit) {
          break;
        }
      }
    }
  } else {
    const fuse = new Fuse(media, fuseOptions);
    suggestions = fuse.search(value, { limit });
  }

  return suggestions;
}

onmessage = function (e) {
  if (!e) {
    return;
  }

  const { media, value } = e.data;

  const suggestions = getSuggestions(media, value);

  const results = {
    value,
    suggestions,
  };

  self.postMessage(results);
};
