import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import * as commandNames from 'Commands/commandNames';
import Alert from 'Components/Alert';
import CheckInput from 'Components/Form/CheckInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import useSelectState from 'Helpers/Hooks/useSelectState';
import { kinds } from 'Helpers/Props';
import formatSeason from 'Season/formatSeason';
import useSeries from 'Series/useSeries';
import { executeCommand } from 'Store/Actions/commandActions';
import { fetchSeriesOrganizePreview } from 'Store/Actions/seriesOrganizePreviewActions';
import { fetchTvNamingSettings } from 'Store/Actions/settingsActions';
import { CheckInputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import translate from 'Utilities/String/translate';
import getSelectedIds from 'Utilities/Table/getSelectedIds';
import SeriesOrganizePreviewRow from './SeriesOrganizePreviewRow';
import styles from './SeriesOrganizePreviewModalContent.css';

function getValue(allSelected: boolean, allUnselected: boolean) {
  if (allSelected) {
    return true;
  } else if (allUnselected) {
    return false;
  }

  return null;
}

export interface SeriesOrganizePreviewModalContentProps {
  seriesId: number;
  seasonNumber?: number;
  onModalClose: () => void;
}

function SeriesOrganizePreviewModalContent({
  seriesId,
  seasonNumber,
  onModalClose,
}: SeriesOrganizePreviewModalContentProps) {
  const dispatch = useDispatch();
  const {
    items,
    isFetching: isPreviewFetching,
    isPopulated: isPreviewPopulated,
    error: previewError,
  } = useSelector((state: AppState) => state.seriesOrganizePreview);

  const {
    isFetching: isNamingFetching,
    isPopulated: isNamingPopulated,
    error: namingError,
    item: naming,
  } = useSelector((state: AppState) => state.settings.tvNaming);

  const series = useSeries(seriesId)!;
  const [selectState, setSelectState] = useSelectState();

  const { allSelected, allUnselected, selectedState } = selectState;
  const isFetching = isPreviewFetching || isNamingFetching;
  const isPopulated = isPreviewPopulated && isNamingPopulated;
  const error = previewError || namingError;
  const { renameEpisodes } = naming;
  const episodeFormat = naming[`${series.seriesType}EpisodeFormat`];

  const selectAllValue = getValue(allSelected, allUnselected);

  const handleSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      setSelectState({ type: value ? 'selectAll' : 'unselectAll', items });
    },
    [items, setSelectState]
  );

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey = false }: SelectStateInputProps) => {
      setSelectState({
        type: 'toggleSelected',
        items,
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [items, setSelectState]
  );

  const handleOrganizePress = useCallback(() => {
    const files = getSelectedIds(selectedState);

    dispatch(
      executeCommand({
        name: commandNames.RENAME_EPISODE_FILES,
        files,
        seriesId,
      })
    );

    onModalClose();
  }, [seriesId, selectedState, dispatch, onModalClose]);

  useEffect(() => {
    dispatch(fetchSeriesOrganizePreview({ seriesId, seasonNumber }));
    dispatch(fetchTvNamingSettings());
  }, [seriesId, seasonNumber, dispatch]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>
        {seasonNumber == null
          ? translate('OrganizeModalHeader')
          : translate('OrganizeModalHeaderSeason', {
              season: formatSeason(seasonNumber) ?? '',
            })}
      </ModalHeader>

      <ModalBody>
        {isFetching ? <LoadingIndicator /> : null}

        {!isFetching && error ? (
          <Alert kind={kinds.DANGER}>{translate('OrganizeLoadError')}</Alert>
        ) : null}

        {!isFetching && isPopulated && !items.length ? (
          <div>
            {renameEpisodes ? (
              <div>{translate('OrganizeNothingToRename')}</div>
            ) : (
              <div>{translate('OrganizeRenamingDisabled')}</div>
            )}
          </div>
        ) : null}

        {!isFetching && isPopulated && items.length ? (
          <div>
            <Alert>
              <div>
                <InlineMarkdown
                  data={translate('OrganizeRelativePaths', {
                    path: series.path,
                  })}
                  blockClassName={styles.path}
                />
              </div>

              <div>
                <InlineMarkdown
                  data={translate('OrganizeNamingPattern', { episodeFormat })}
                  blockClassName={styles.episodeFormat}
                />
              </div>
            </Alert>

            <div className={styles.previews}>
              {items.map((item) => {
                return (
                  <SeriesOrganizePreviewRow
                    key={item.episodeFileId}
                    id={item.episodeFileId}
                    existingPath={item.existingPath}
                    newPath={item.newPath}
                    isSelected={selectedState[item.episodeFileId]}
                    onSelectedChange={handleSelectedChange}
                  />
                );
              })}
            </div>
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        {isPopulated && items.length ? (
          <CheckInput
            className={styles.selectAllInput}
            containerClassName={styles.selectAllInputContainer}
            name="selectAll"
            value={selectAllValue}
            onChange={handleSelectAllChange}
          />
        ) : null}

        <Button onPress={onModalClose}>{translate('Cancel')}</Button>

        <Button kind={kinds.PRIMARY} onPress={handleOrganizePress}>
          {translate('Organize')}
        </Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default SeriesOrganizePreviewModalContent;
