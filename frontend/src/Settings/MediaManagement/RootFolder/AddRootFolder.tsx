import React, { useCallback, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Alert from 'Components/Alert';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import Icon from 'Components/Icon';
import Button from 'Components/Link/Button';
import { icons, kinds, sizes } from 'Helpers/Props';
import { addRootFolder } from 'Store/Actions/rootFolderActions';
import { addTvRootFolder } from 'Store/Actions/tvRootFolderActions';
import createRootFoldersSelector from 'Store/Selectors/createRootFoldersSelector';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import styles from './AddRootFolder.css';

interface AddRootFolderProps {
  mediaKind?: MediaKind;
}

function AddRootFolder({ mediaKind = 'movies' }: AddRootFolderProps) {
  const { isSaving, saveError } = useSelector(
    createRootFoldersSelector(
      mediaKind === 'tv' ? 'tvRootFolders' : 'rootFolders'
    )
  );

  const dispatch = useDispatch();

  const [isAddNewRootFolderModalOpen, setIsAddNewRootFolderModalOpen] =
    useState(false);

  const onAddNewRootFolderPress = useCallback(() => {
    setIsAddNewRootFolderModalOpen(true);
  }, [setIsAddNewRootFolderModalOpen]);

  const onNewRootFolderSelect = useCallback(
    ({ value }: { value: string }) => {
      dispatch(
        mediaKind === 'tv'
          ? addTvRootFolder({ path: value })
          : addRootFolder({ path: value })
      );
    },
    [dispatch, mediaKind]
  );

  const onAddRootFolderModalClose = useCallback(() => {
    setIsAddNewRootFolderModalOpen(false);
  }, [setIsAddNewRootFolderModalOpen]);

  return (
    <>
      {!isSaving && saveError ? (
        <Alert kind={kinds.DANGER}>
          {translate('AddRootFolderError')}

          <ul>
            {Array.isArray(saveError.responseJSON) ? (
              saveError.responseJSON.map((e, index) => {
                return <li key={index}>{e.errorMessage}</li>;
              })
            ) : (
              <li>{JSON.stringify(saveError.responseJSON)}</li>
            )}
          </ul>
        </Alert>
      ) : null}

      <div className={styles.addRootFolderButtonContainer}>
        <Button
          kind={kinds.PRIMARY}
          size={sizes.LARGE}
          onPress={onAddNewRootFolderPress}
        >
          <Icon className={styles.importButtonIcon} name={icons.DRIVE} />
          {translate('AddRootFolder')}
        </Button>

        <FileBrowserModal
          isOpen={isAddNewRootFolderModalOpen}
          name="rootFolderPath"
          value=""
          onChange={onNewRootFolderSelect}
          onModalClose={onAddRootFolderModalClose}
        />
      </div>
    </>
  );
}

export default AddRootFolder;
