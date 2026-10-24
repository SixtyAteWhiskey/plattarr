import React, { ComponentType } from 'react';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import { useMediaKind } from 'Helpers/mediaKindStore';
import translate from 'Utilities/String/translate';

// Builds a page that renders the movie, series or music version of a shared
// screen, following the kind the media type switcher selected. A kind without
// a version of the screen says so instead of showing another kind's data.
function createMediaKindPage(
  movies: ComponentType,
  tv: ComponentType,
  music?: ComponentType
) {
  const Movies = movies;
  const Tv = tv;
  const Music = music;

  function MediaKindPage() {
    const mediaKind = useMediaKind();

    if (mediaKind === 'tv') {
      return <Tv />;
    }

    if (mediaKind === 'music') {
      return Music ? (
        <Music />
      ) : (
        <PageContent title={translate('Music')}>
          <PageContentBody>{translate('NotAvailableForMusic')}</PageContentBody>
        </PageContent>
      );
    }

    return <Movies />;
  }

  return MediaKindPage;
}

export default createMediaKindPage;
