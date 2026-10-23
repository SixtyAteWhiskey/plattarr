import React from 'react';
import Modal from 'Components/Modal/Modal';
import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import { sizes } from 'Helpers/Props';
import Language from 'Language/Language';
import { QualityModel } from 'Quality/Quality';
import { ReleaseEpisode } from 'typings/TvRelease';
import TvOverrideMatchModalContent from './TvOverrideMatchModalContent';

interface TvOverrideMatchModalProps {
  isOpen: boolean;
  title: string;
  indexerId: number;
  guid: string;
  seriesId?: number;
  seasonNumber?: number;
  episodes: ReleaseEpisode[];
  languages: Language[];
  quality: QualityModel;
  protocol: DownloadProtocol;
  isGrabbing: boolean;
  grabError?: string;
  onModalClose(): void;
}

function TvOverrideMatchModal(props: TvOverrideMatchModalProps) {
  const {
    isOpen,
    title,
    indexerId,
    guid,
    seriesId,
    seasonNumber,
    episodes,
    languages,
    quality,
    protocol,
    isGrabbing,
    grabError,
    onModalClose,
  } = props;

  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={onModalClose}>
      <TvOverrideMatchModalContent
        title={title}
        indexerId={indexerId}
        guid={guid}
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        episodes={episodes}
        languages={languages}
        quality={quality}
        protocol={protocol}
        isGrabbing={isGrabbing}
        grabError={grabError}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default TvOverrideMatchModal;
