import React, { useCallback, useEffect, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import FileBrowserModal from 'Components/FileBrowser/FileBrowserModal';
import usePrevious from 'Helpers/Hooks/usePrevious';
import EditMusicRootFolderModalConnector from 'Settings/MediaManagement/RootFolder/Music/EditRootFolderModalConnector';
import {
  addRootFolder,
  fetchRootFolders,
} from 'Store/Actions/rootFolderActions';
import { fetchMusicRootFolders } from 'Store/Actions/settingsActions';
import {
  addTvRootFolder,
  fetchTvRootFolders,
} from 'Store/Actions/tvRootFolderActions';
import createRootFoldersSelector from 'Store/Selectors/createRootFoldersSelector';
import { EnhancedSelectInputChanged, InputChanged } from 'typings/inputs';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';
import RootFolderSelectInputOption from './RootFolderSelectInputOption';
import RootFolderSelectInputSelectedValue from './RootFolderSelectInputSelectedValue';

const ADD_NEW_KEY = 'addNew';

export interface RootFolderSelectInputValue
  extends EnhancedSelectInputValue<string> {
  freeSpace?: number;
  isMissing?: boolean;
}

export interface RootFolderSelectInputProps
  extends Omit<
    EnhancedSelectInputProps<EnhancedSelectInputValue<string>, string>,
    'value' | 'values'
  > {
  name: string;
  value?: string;
  includeMissingValue?: boolean;
  includeNoChange?: boolean;
  includeNoChangeDisabled?: boolean;
  mediaKind?: MediaKind;
}

function createRootFolderOptionsSelector(
  value: string | undefined,
  includeMissingValue: boolean,
  includeNoChange: boolean,
  includeNoChangeDisabled: boolean,
  mediaKind: MediaKind
) {
  const sections = {
    movies: 'rootFolders',
    tv: 'tvRootFolders',
    music: 'settings.musicRootFolders',
  } as const;
  const section = sections[mediaKind];

  return createSelector(createRootFoldersSelector(section), (rootFolders) => {
    const values: RootFolderSelectInputValue[] = rootFolders.items.map(
      (rootFolder) => {
        return {
          key: rootFolder.path,
          value: rootFolder.path,
          freeSpace: rootFolder.freeSpace,
          isMissing: false,
        };
      }
    );

    if (includeNoChange) {
      values.unshift({
        key: 'noChange',
        get value() {
          return translate('NoChange');
        },
        isDisabled: includeNoChangeDisabled,
        isMissing: false,
      });
    }

    if (!values.length) {
      values.push({
        key: '',
        value: '',
        isDisabled: true,
        isHidden: true,
      });
    }

    if (includeMissingValue && value && !values.find((v) => v.key === value)) {
      values.push({
        key: value,
        value,
        isMissing: true,
        isDisabled: true,
      });
    }

    values.push({
      key: ADD_NEW_KEY,
      value: translate('AddANewPath'),
    });

    return {
      values,
      isSaving: rootFolders.isSaving,
      saveError: rootFolders.saveError,
    };
  });
}

function RootFolderSelectInput({
  name,
  value,
  includeMissingValue = true,
  includeNoChange = false,
  includeNoChangeDisabled = true,
  mediaKind = 'movies',
  onChange,
  ...otherProps
}: RootFolderSelectInputProps) {
  const dispatch = useDispatch();
  const { values, isSaving, saveError } = useSelector(
    createRootFolderOptionsSelector(
      value,
      includeMissingValue,
      includeNoChange,
      includeNoChangeDisabled,
      mediaKind
    )
  );
  const [isAddNewRootFolderModalOpen, setIsAddNewRootFolderModalOpen] =
    useState(false);
  const [isAddMusicRootFolderModalOpen, setIsAddMusicRootFolderModalOpen] =
    useState(false);
  const [newRootFolderPath, setNewRootFolderPath] = useState('');
  const previousIsSaving = usePrevious(isSaving);

  const handleChange = useCallback(
    ({ value: newValue }: EnhancedSelectInputChanged<string>) => {
      if (newValue === 'addNew') {
        // A Music root folder needs a name and default profiles, so it opens
        // the full root folder form instead of the bare path browser.
        if (mediaKind === 'music') {
          setIsAddMusicRootFolderModalOpen(true);
        } else {
          setIsAddNewRootFolderModalOpen(true);
        }
      } else {
        onChange({ name, value: newValue });
      }
    },
    [
      name,
      mediaKind,
      setIsAddNewRootFolderModalOpen,
      setIsAddMusicRootFolderModalOpen,
      onChange,
    ]
  );

  const handleMusicRootFolderAdded = useCallback(
    (path: string) => {
      setNewRootFolderPath(path);
    },
    [setNewRootFolderPath]
  );

  const handleAddMusicRootFolderModalClose = useCallback(() => {
    setIsAddMusicRootFolderModalOpen(false);
  }, [setIsAddMusicRootFolderModalOpen]);

  const handleNewRootFolderSelect = useCallback(
    ({ value: newValue }: InputChanged<string>) => {
      setNewRootFolderPath(newValue);
      if (mediaKind === 'music') {
        return;
      }

      dispatch(
        mediaKind === 'tv'
          ? addTvRootFolder({ path: newValue })
          : addRootFolder({ path: newValue })
      );
    },
    [setNewRootFolderPath, mediaKind, dispatch]
  );

  const handleAddRootFolderModalClose = useCallback(() => {
    setIsAddNewRootFolderModalOpen(false);
  }, [setIsAddNewRootFolderModalOpen]);

  useEffect(() => {
    if (
      !value &&
      values.length &&
      values.some((v) => !!v.key && v.key !== ADD_NEW_KEY)
    ) {
      const defaultValue = values[0];

      if (defaultValue.key !== ADD_NEW_KEY) {
        onChange({ name, value: defaultValue.key });
      }
    }

    if (previousIsSaving && !isSaving && !saveError && newRootFolderPath) {
      onChange({ name, value: newRootFolderPath });
      setNewRootFolderPath('');
    }
  }, [
    name,
    value,
    values,
    isSaving,
    saveError,
    previousIsSaving,
    newRootFolderPath,
    onChange,
  ]);

  useEffect(() => {
    if (value == null && values[0].key === '') {
      onChange({ name, value: '' });
    } else if (
      !value ||
      !values.some((v) => v.key === value) ||
      value === ADD_NEW_KEY
    ) {
      const defaultValue = values[0];

      if (defaultValue.key === ADD_NEW_KEY) {
        onChange({ name, value: '' });
      } else {
        onChange({ name, value: defaultValue.key });
      }
    }

    // Only run on mount
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (mediaKind === 'tv') {
      dispatch(fetchTvRootFolders());
    } else if (mediaKind === 'music') {
      dispatch(fetchMusicRootFolders());
    } else {
      dispatch(fetchRootFolders());
    }
  }, [mediaKind, dispatch]);

  return (
    <>
      <EnhancedSelectInput
        {...otherProps}
        name={name}
        value={value ?? ''}
        values={values}
        selectedValueComponent={RootFolderSelectInputSelectedValue}
        optionComponent={RootFolderSelectInputOption}
        onChange={handleChange}
      />

      <FileBrowserModal
        isOpen={isAddNewRootFolderModalOpen}
        name="rootFolderPath"
        value=""
        onChange={handleNewRootFolderSelect}
        onModalClose={handleAddRootFolderModalClose}
      />

      {mediaKind === 'music' ? (
        <EditMusicRootFolderModalConnector
          isOpen={isAddMusicRootFolderModalOpen}
          onRootFolderAdded={handleMusicRootFolderAdded}
          onModalClose={handleAddMusicRootFolderModalClose}
        />
      ) : null}
    </>
  );
}

export default RootFolderSelectInput;
