import React from 'react';
import Modal from 'Components/Modal/Modal';
import MediaKind from 'typings/MediaKind';
import AddSpecificationModalContent from './AddSpecificationModalContent';

interface AddSpecificationModalProps {
  isOpen: boolean;
  mediaKind?: MediaKind;
  onModalClose: (options?: { specificationSelected: boolean }) => void;
}

function AddSpecificationModal({
  isOpen,
  mediaKind,
  onModalClose,
}: AddSpecificationModalProps) {
  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <AddSpecificationModalContent
        mediaKind={mediaKind}
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default AddSpecificationModal;
