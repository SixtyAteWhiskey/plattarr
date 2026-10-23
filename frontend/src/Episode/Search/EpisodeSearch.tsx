import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import * as commandNames from 'Commands/commandNames';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import { icons, kinds, sizes } from 'Helpers/Props';
import TvInteractiveSearch from 'InteractiveSearch/Tv/TvInteractiveSearch';
import { executeCommand } from 'Store/Actions/commandActions';
import translate from 'Utilities/String/translate';
import styles from './EpisodeSearch.css';

interface EpisodeSearchProps {
  episodeId: number;
  startInteractiveSearch: boolean;
  onModalClose: () => void;
}

function EpisodeSearch({
  episodeId,
  startInteractiveSearch,
  onModalClose,
}: EpisodeSearchProps) {
  const dispatch = useDispatch();
  const { isPopulated } = useSelector((state: AppState) => state.tvReleases);

  const [isInteractiveSearchOpen, setIsInteractiveSearchOpen] = useState(
    startInteractiveSearch || isPopulated
  );

  const handleQuickSearchPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: commandNames.EPISODE_SEARCH,
        episodeIds: [episodeId],
      })
    );

    onModalClose();
  }, [episodeId, dispatch, onModalClose]);

  const handleInteractiveSearchPress = useCallback(() => {
    setIsInteractiveSearchOpen(true);
  }, []);

  if (isInteractiveSearchOpen) {
    return <TvInteractiveSearch type="episode" searchPayload={{ episodeId }} />;
  }

  return (
    <div>
      <div className={styles.buttonContainer}>
        <Button
          className={styles.button}
          size={sizes.LARGE}
          onPress={handleQuickSearchPress}
        >
          <Icon className={styles.buttonIcon} name={icons.QUICK} />

          {translate('QuickSearch')}
        </Button>
      </div>

      <div className={styles.buttonContainer}>
        <Button
          className={styles.button}
          kind={kinds.PRIMARY}
          size={sizes.LARGE}
          onPress={handleInteractiveSearchPress}
        >
          <Icon className={styles.buttonIcon} name={icons.INTERACTIVE} />

          {translate('InteractiveSearch')}
        </Button>
      </div>
    </div>
  );
}

export default EpisodeSearch;
