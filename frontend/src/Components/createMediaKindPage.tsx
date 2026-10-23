import React, { ComponentType } from 'react';
import { useMediaKind } from 'Helpers/mediaKindStore';

// Builds a page that renders the movie or the series version of a shared
// screen, following the kind the media type switcher selected.
function createMediaKindPage(movies: ComponentType, tv: ComponentType) {
  const Movies = movies;
  const Tv = tv;

  function MediaKindPage() {
    const mediaKind = useMediaKind();

    return mediaKind === 'tv' ? <Tv /> : <Movies />;
  }

  return MediaKindPage;
}

export default createMediaKindPage;
