import React from 'react';
import { useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Modal from 'Components/Modal/Modal';
import ManageTagsModalContent from 'Settings/Tags/ManageTagsModalContent';

interface TagsModalProps {
  isOpen: boolean;
  ids: number[];
  onApplyTagsPress: (tags: number[], applyTags: string) => void;
  onModalClose: () => void;
}

function TagsModal(props: TagsModalProps) {
  const { isOpen, onModalClose, ...otherProps } = props;
  const items = useSelector(
    (state: AppState) => state.settings.downloadClients.items
  );

  return (
    <Modal isOpen={isOpen} onModalClose={onModalClose}>
      <ManageTagsModalContent
        {...otherProps}
        items={items}
        helpTextKey="ApplyTagsHelpTextHowToApplyDownloadClients"
        onModalClose={onModalClose}
      />
    </Modal>
  );
}

export default TagsModal;
