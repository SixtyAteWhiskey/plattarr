import { createPersist } from 'Helpers/createPersist';
import MediaKind from 'typings/MediaKind';

interface MediaKindState {
  mediaKind: MediaKind;
}

// The media kind the shared pages (history, blocklist, wanted, calendar)
// are showing. The media type switcher writes it; pages only read it.
const mediaKindStore = createPersist<MediaKindState>('media_kind', () => {
  return {
    mediaKind: 'movies',
  };
});

export const useMediaKind = (): MediaKind => {
  return mediaKindStore((state) => state.mediaKind);
};

export const setMediaKind = (mediaKind: MediaKind) => {
  mediaKindStore.setState({ mediaKind });
};
