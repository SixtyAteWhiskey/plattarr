import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import Modal from 'Components/Modal/Modal';
import { sizes } from 'Helpers/Props';
import { clearPendingChanges } from 'Store/Actions/baseActions';
import EditAutoTaggingModalContent, {
  EditAutoTaggingModalContentProps,
} from './EditAutoTaggingModalContent';

interface EditAutoTaggingModalProps extends EditAutoTaggingModalContentProps {
  isOpen: boolean;
  onModalClose: () => void;
}

export default function EditAutoTaggingModal({
  isOpen,
  onModalClose,
  ...otherProps
}: EditAutoTaggingModalProps) {
  const dispatch = useDispatch();

  const handleModalClose = useCallback(() => {
    dispatch(
      clearPendingChanges({
        section:
          otherProps.mediaKind === 'tv'
            ? 'settings.tvAutoTaggings'
            : 'settings.autoTaggings',
      })
    );
    onModalClose();
  }, [dispatch, onModalClose, otherProps.mediaKind]);

  return (
    <Modal isOpen={isOpen} size={sizes.LARGE} onModalClose={handleModalClose}>
      <EditAutoTaggingModalContent
        {...otherProps}
        onModalClose={handleModalClose}
      />
    </Modal>
  );
}
