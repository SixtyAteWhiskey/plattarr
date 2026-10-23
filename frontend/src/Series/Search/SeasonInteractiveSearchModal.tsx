import React, { useCallback, useEffect } from 'react';
import { useDispatch } from 'react-redux';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import {
  cancelFetchTvReleases,
  clearTvReleases,
} from 'Store/Actions/tvReleaseActions';
import SeasonInteractiveSearchModalContent from './SeasonInteractiveSearchModalContent';

interface SeasonInteractiveSearchModalProps {
  isOpen: boolean;
  seriesId: number;
  seasonNumber: number;
  onModalClose(): void;
}

function SeasonInteractiveSearchModal(
  props: SeasonInteractiveSearchModalProps
) {
  const { isOpen, seriesId, seasonNumber, onModalClose } = props;

  const dispatch = useDispatch();

  const handleModalClose = useCallback(() => {
    onModalClose();

    dispatch(cancelFetchTvReleases());
    dispatch(clearTvReleases());
  }, [dispatch, onModalClose]);

  useEffect(() => {
    return () => {
      dispatch(cancelFetchTvReleases());
      dispatch(clearTvReleases());
    };
  }, [dispatch]);

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={false}
      onModalClose={handleModalClose}
    >
      <SeasonInteractiveSearchModalContent
        seriesId={seriesId}
        seasonNumber={seasonNumber}
        onModalClose={handleModalClose}
      />
    </Modal>
  );
}

export default SeasonInteractiveSearchModal;
