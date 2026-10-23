import React, { useCallback, useEffect, useState } from 'react';
import Modal from 'Components/Modal/Modal';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { sizes } from 'Helpers/Props';
import InteractiveImportSelectFolderModalContent from 'InteractiveImport/Folder/InteractiveImportSelectFolderModalContent';
import translate from 'Utilities/String/translate';
import TvInteractiveImportModalContent, {
  TvInteractiveImportModalContentProps,
} from './TvInteractiveImportModalContent';

interface TvInteractiveImportModalProps
  extends Omit<TvInteractiveImportModalContentProps, 'modalTitle'> {
  isOpen: boolean;
  folder?: string;
  downloadId?: string;
  modalTitle?: string;
  onModalClose(): void;
}

function TvInteractiveImportModal(props: TvInteractiveImportModalProps) {
  const {
    isOpen,
    folder,
    downloadId,
    modalTitle = translate('ManualImport'),
    onModalClose,
    ...otherProps
  } = props;

  const [folderPath, setFolderPath] = useState<string | undefined>(folder);
  const previousIsOpen = usePrevious(isOpen);

  const onFolderSelect = useCallback(
    (path: string) => {
      setFolderPath(path);
    },
    [setFolderPath]
  );

  useEffect(() => {
    setFolderPath(folder);
  }, [folder, setFolderPath]);

  useEffect(() => {
    if (previousIsOpen && !isOpen) {
      setFolderPath(folder);
    }
  }, [folder, previousIsOpen, isOpen, setFolderPath]);

  return (
    <Modal
      isOpen={isOpen}
      size={sizes.EXTRA_EXTRA_LARGE}
      closeOnBackgroundClick={false}
      onModalClose={onModalClose}
    >
      {folderPath || downloadId ? (
        <TvInteractiveImportModalContent
          {...otherProps}
          folder={folderPath}
          downloadId={downloadId}
          modalTitle={modalTitle}
          onModalClose={onModalClose}
        />
      ) : (
        <InteractiveImportSelectFolderModalContent
          {...otherProps}
          mediaKind="tv"
          modalTitle={modalTitle}
          onFolderSelect={onFolderSelect}
          onModalClose={onModalClose}
        />
      )}
    </Modal>
  );
}

export default TvInteractiveImportModal;
