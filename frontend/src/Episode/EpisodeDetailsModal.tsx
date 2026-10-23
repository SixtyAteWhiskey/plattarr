import React from 'react';
import Modal from 'Components/Modal/Modal';
import EpisodeDetailsTab from 'Episode/EpisodeDetailsTab';
import { EpisodeEntity } from 'Episode/useEpisode';
import { sizes } from 'Helpers/Props';
import EpisodeDetailsModalContent from './EpisodeDetailsModalContent';

interface EpisodeDetailsModalProps {
  isOpen: boolean;
  episodeId: number;
  episodeEntity: EpisodeEntity;
  seriesId: number;
  episodeTitle: string;
  isSaving?: boolean;
  showOpenSeriesButton?: boolean;
  selectedTab?: EpisodeDetailsTab;
  onModalClose(): void;
}

function EpisodeDetailsModal(props: EpisodeDetailsModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      onModalClose={onModalClose}
    >
      <EpisodeDetailsModalContent {...otherProps} onModalClose={onModalClose} />
    </Modal>
  );
}

export default EpisodeDetailsModal;
