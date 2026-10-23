import React from 'react';
import Button from 'Components/Link/Button';
import SpinnerButton from 'Components/Link/SpinnerButton';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import { HistoryData, HistoryEventType } from 'typings/History';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import HistoryDetails from './HistoryDetails';
import styles from './HistoryDetailsModal.css';

function getHeaderTitle(eventType: HistoryEventType, mediaKind: MediaKind) {
  const isTv = mediaKind === 'tv';

  switch (eventType) {
    case 'grabbed':
      return translate('Grabbed');
    case 'downloadFailed':
      return translate('DownloadFailed');
    case 'downloadFolderImported':
      return translate(isTv ? 'EpisodeImported' : 'MovieImported');
    case 'movieFileDeleted':
    case 'episodeFileDeleted':
      return translate(isTv ? 'EpisodeFileDeleted' : 'MovieFileDeleted');
    case 'movieFileRenamed':
    case 'episodeFileRenamed':
      return translate(isTv ? 'EpisodeFileRenamed' : 'MovieFileRenamed');
    case 'downloadIgnored':
      return translate('DownloadIgnored');
    default:
      return translate('Unknown');
  }
}

interface HistoryDetailsModalProps {
  isOpen: boolean;
  eventType: HistoryEventType;
  sourceTitle: string;
  data: HistoryData;
  downloadId?: string;
  isMarkingAsFailed?: boolean;
  mediaKind?: MediaKind;
  onMarkAsFailedPress: () => void;
  onModalClose: () => void;
}

function HistoryDetailsModal(props: HistoryDetailsModalProps) {
  const {
    isOpen,
    eventType,
    sourceTitle,
    data,
    downloadId,
    isMarkingAsFailed = false,
    mediaKind = 'movies',
    onMarkAsFailedPress,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ModalContent onModalClose={onModalClose}>
        <ModalHeader>{getHeaderTitle(eventType, mediaKind)}</ModalHeader>

        <ModalBody>
          <HistoryDetails
            eventType={eventType}
            sourceTitle={sourceTitle}
            data={data}
            downloadId={downloadId}
          />
        </ModalBody>

        <ModalFooter>
          {eventType === 'grabbed' && (
            <SpinnerButton
              className={styles.markAsFailedButton}
              kind={kinds.DANGER}
              isSpinning={isMarkingAsFailed}
              onPress={onMarkAsFailedPress}
            >
              {translate('MarkAsFailed')}
            </SpinnerButton>
          )}

          <Button onPress={onModalClose}>{translate('Close')}</Button>
        </ModalFooter>
      </ModalContent>
    </Modal>
  );
}

export default HistoryDetailsModal;
