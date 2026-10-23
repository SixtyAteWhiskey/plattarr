import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { EPISODE_SEARCH } from 'Commands/commandNames';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import { icons } from 'Helpers/Props';
import { executeCommand } from 'Store/Actions/commandActions';
import createExecutingCommandsSelector from 'Store/Selectors/createExecutingCommandsSelector';
import translate from 'Utilities/String/translate';
import styles from './EpisodeSearchCell.css';

interface EpisodeSearchCellProps {
  episodeId: number;
}

function EpisodeSearchCell({ episodeId }: EpisodeSearchCellProps) {
  const executingCommands = useSelector(createExecutingCommandsSelector());
  const isSearching = executingCommands.some(({ name, body }) => {
    const { episodeIds = [] } = body;
    return name === EPISODE_SEARCH && episodeIds.indexOf(episodeId) > -1;
  });

  const dispatch = useDispatch();

  const handleSearchPress = useCallback(() => {
    dispatch(
      executeCommand({
        name: EPISODE_SEARCH,
        episodeIds: [episodeId],
      })
    );
  }, [episodeId, dispatch]);

  return (
    <TableRowCell className={styles.episodeSearchCell}>
      <SpinnerIconButton
        name={icons.SEARCH}
        isSpinning={isSearching}
        title={translate('AutomaticSearch')}
        onPress={handleSearchPress}
      />
    </TableRowCell>
  );
}

export default EpisodeSearchCell;
