import React, { useCallback, useState } from 'react';
import { useSelector } from 'react-redux';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import ConfirmModal from 'Components/Modal/ConfirmModal';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRow from 'Components/Table/TableRow';
import Popover from 'Components/Tooltip/Popover';
import Tooltip from 'Components/Tooltip/Tooltip';
import EpisodeFormats from 'Episode/EpisodeFormats';
import EpisodeLanguages from 'Episode/EpisodeLanguages';
import EpisodeQuality from 'Episode/EpisodeQuality';
import { icons, kinds, tooltipPositions } from 'Helpers/Props';
import Peers from 'InteractiveSearch/Peers';
import createUISettingsSelector from 'Store/Selectors/createUISettingsSelector';
import TvRelease from 'typings/TvRelease';
import formatDateTime from 'Utilities/Date/formatDateTime';
import formatAge from 'Utilities/Number/formatAge';
import formatBytes from 'Utilities/Number/formatBytes';
import formatCustomFormatScore from 'Utilities/Number/formatCustomFormatScore';
import translate from 'Utilities/String/translate';
import ReleaseSceneIndicator from './ReleaseSceneIndicator';
import TvInteractiveSearchPayload from './TvInteractiveSearchPayload';
import styles from './TvInteractiveSearchRow.css';

function getDownloadIcon(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return icons.SPINNER;
  } else if (isGrabbed) {
    return icons.DOWNLOADING;
  } else if (grabError) {
    return icons.DOWNLOADING;
  }

  return icons.DOWNLOAD;
}

function getDownloadKind(isGrabbed: boolean, grabError?: string) {
  if (isGrabbed) {
    return kinds.SUCCESS;
  }

  if (grabError) {
    return kinds.DANGER;
  }

  return kinds.DEFAULT;
}

function getDownloadTooltip(
  isGrabbing: boolean,
  isGrabbed: boolean,
  grabError?: string
) {
  if (isGrabbing) {
    return '';
  } else if (isGrabbed) {
    return translate('AddedToDownloadQueue');
  } else if (grabError) {
    return grabError;
  }

  return translate('AddToDownloadQueue');
}

interface TvInteractiveSearchRowProps extends TvRelease {
  searchPayload: TvInteractiveSearchPayload;
  onGrabPress(...args: unknown[]): void;
}

function TvInteractiveSearchRow(props: TvInteractiveSearchRowProps) {
  const {
    guid,
    indexerId,
    protocol,
    age,
    ageHours,
    ageMinutes,
    publishDate,
    title,
    infoUrl,
    indexer,
    size,
    seeders,
    leechers,
    quality,
    languages,
    customFormatScore,
    customFormats,
    sceneMapping,
    seasonNumber,
    episodeNumbers,
    absoluteEpisodeNumbers,
    mappedSeasonNumber,
    mappedEpisodeNumbers,
    mappedAbsoluteEpisodeNumbers,
    indexerFlags = [],
    rejections = [],
    episodeRequested,
    downloadAllowed,
    isDaily,
    isGrabbing = false,
    isGrabbed = false,
    grabError,
    searchPayload,
    onGrabPress,
  } = props;

  const { longDateFormat, timeFormat } = useSelector(
    createUISettingsSelector()
  );

  const [isConfirmGrabModalOpen, setIsConfirmGrabModalOpen] = useState(false);

  const onGrabPressWrapper = useCallback(() => {
    if (downloadAllowed) {
      onGrabPress({
        guid,
        indexerId,
      });

      return;
    }

    setIsConfirmGrabModalOpen(true);
  }, [
    guid,
    indexerId,
    downloadAllowed,
    onGrabPress,
    setIsConfirmGrabModalOpen,
  ]);

  const onGrabConfirm = useCallback(() => {
    setIsConfirmGrabModalOpen(false);

    onGrabPress({
      guid,
      indexerId,
      ...searchPayload,
    });
  }, [guid, indexerId, searchPayload, onGrabPress, setIsConfirmGrabModalOpen]);

  const onGrabCancel = useCallback(() => {
    setIsConfirmGrabModalOpen(false);
  }, [setIsConfirmGrabModalOpen]);

  return (
    <TableRow>
      <TableRowCell className={styles.protocol}>
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell
        className={styles.age}
        title={formatDateTime(publishDate, longDateFormat, timeFormat, {
          includeSeconds: true,
        })}
      >
        {formatAge(age, ageHours, ageMinutes)}
      </TableRowCell>

      <TableRowCell>
        <div className={styles.titleContent}>
          <Link to={infoUrl} title={title}>
            {title}
          </Link>
          <ReleaseSceneIndicator
            className={styles.sceneMapping}
            seasonNumber={mappedSeasonNumber}
            episodeNumbers={mappedEpisodeNumbers}
            absoluteEpisodeNumbers={mappedAbsoluteEpisodeNumbers}
            sceneSeasonNumber={seasonNumber}
            sceneEpisodeNumbers={episodeNumbers}
            sceneAbsoluteEpisodeNumbers={absoluteEpisodeNumbers}
            sceneMapping={sceneMapping ?? undefined}
            episodeRequested={episodeRequested}
            isDaily={isDaily}
          />
        </div>
      </TableRowCell>

      <TableRowCell className={styles.indexer}>{indexer}</TableRowCell>

      <TableRowCell className={styles.size}>{formatBytes(size)}</TableRowCell>

      <TableRowCell className={styles.peers}>
        {protocol === 'torrent' ? (
          <Peers seeders={seeders} leechers={leechers} />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.languages}>
        <EpisodeLanguages languages={languages} />
      </TableRowCell>

      <TableRowCell className={styles.quality}>
        <EpisodeQuality quality={quality} showRevision={true} />
      </TableRowCell>

      <TableRowCell className={styles.customFormatScore}>
        <Tooltip
          anchor={formatCustomFormatScore(
            customFormatScore,
            customFormats.length
          )}
          tooltip={<EpisodeFormats formats={customFormats} />}
          position={tooltipPositions.LEFT}
        />
      </TableRowCell>

      <TableRowCell className={styles.indexerFlags}>
        {indexerFlags.length ? (
          <Popover
            anchor={<Icon name={icons.FLAG} />}
            title={translate('IndexerFlags')}
            body={
              <ul>
                {indexerFlags.map((flag, index) => {
                  return <li key={index}>{flag}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.rejected}>
        {rejections.length ? (
          <Popover
            anchor={<Icon name={icons.DANGER} kind={kinds.DANGER} />}
            title={translate('ReleaseRejected')}
            body={
              <ul>
                {rejections.map((rejection, index) => {
                  return <li key={index}>{rejection}</li>;
                })}
              </ul>
            }
            position={tooltipPositions.LEFT}
          />
        ) : null}
      </TableRowCell>

      <TableRowCell className={styles.download}>
        <SpinnerIconButton
          name={getDownloadIcon(isGrabbing, isGrabbed, grabError)}
          kind={getDownloadKind(isGrabbed, grabError)}
          title={getDownloadTooltip(isGrabbing, isGrabbed, grabError)}
          isSpinning={isGrabbing}
          onPress={onGrabPressWrapper}
        />
      </TableRowCell>

      <ConfirmModal
        isOpen={isConfirmGrabModalOpen}
        kind={kinds.WARNING}
        title={translate('GrabRelease')}
        message={translate('GrabReleaseUnknownSeriesOrEpisodeMessageText', {
          title,
        })}
        confirmLabel={translate('Grab')}
        onConfirm={onGrabConfirm}
        onCancel={onGrabCancel}
      />
    </TableRow>
  );
}

export default TvInteractiveSearchRow;
