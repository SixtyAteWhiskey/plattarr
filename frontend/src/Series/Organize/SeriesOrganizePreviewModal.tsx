import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import Modal from 'Components/Modal/Modal';
import { clearSeriesOrganizePreview } from 'Store/Actions/seriesOrganizePreviewActions';
import SeriesOrganizePreviewModalContent, {
  SeriesOrganizePreviewModalContentProps,
} from './SeriesOrganizePreviewModalContent';

interface OrganizePreviewModalProps
  extends SeriesOrganizePreviewModalContentProps {
  isOpen: boolean;
  onModalClose: () => void;
}

function SeriesOrganizePreviewModal({
  isOpen,
  onModalClose,
  ...otherProps
}: OrganizePreviewModalProps) {
  const dispatch = useDispatch();

  const handleOnModalClose = useCallback(() => {
    dispatch(clearSeriesOrganizePreview());
    onModalClose();
  }, [dispatch, onModalClose]);

  return (
    <Modal isOpen={isOpen} onModalClose={handleOnModalClose}>
      {isOpen ? (
        <SeriesOrganizePreviewModalContent
          {...otherProps}
          onModalClose={handleOnModalClose}
        />
      ) : null}
    </Modal>
  );
}
export default SeriesOrganizePreviewModal;
