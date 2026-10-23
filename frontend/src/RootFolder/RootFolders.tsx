import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { kinds } from 'Helpers/Props';
import { fetchRootFolders } from 'Store/Actions/rootFolderActions';
import { fetchTvRootFolders } from 'Store/Actions/tvRootFolderActions';
import createRootFoldersSelector from 'Store/Selectors/createRootFoldersSelector';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import RootFolderRow from './RootFolderRow';

const rootFolderColumns: Column[] = [
  {
    name: 'path',
    label: () => translate('Path'),
    isVisible: true,
  },
  {
    name: 'freeSpace',
    label: () => translate('FreeSpace'),
    isVisible: true,
  },
  {
    name: 'unmappedFolders',
    label: () => translate('UnmappedFolders'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isVisible: true,
  },
];

interface RootFoldersProps {
  mediaKind?: MediaKind;
}

function RootFolders({ mediaKind = 'movies' }: RootFoldersProps) {
  const { isFetching, isPopulated, error, items } = useSelector(
    createRootFoldersSelector(
      mediaKind === 'tv' ? 'tvRootFolders' : 'rootFolders'
    )
  );

  const dispatch = useDispatch();

  useEffect(() => {
    dispatch(mediaKind === 'tv' ? fetchTvRootFolders() : fetchRootFolders());
  }, [dispatch, mediaKind]);

  if (isFetching && !isPopulated) {
    return <LoadingIndicator />;
  }

  if (!isFetching && !!error) {
    return (
      <Alert kind={kinds.DANGER}>{translate('RootFoldersLoadError')}</Alert>
    );
  }

  return (
    <Table columns={rootFolderColumns}>
      <TableBody>
        {items.map((rootFolder) => {
          return (
            <RootFolderRow
              key={rootFolder.id}
              id={rootFolder.id}
              path={rootFolder.path}
              accessible={rootFolder.accessible}
              freeSpace={rootFolder.freeSpace}
              unmappedFolders={rootFolder.unmappedFolders}
              mediaKind={mediaKind}
            />
          );
        })}
      </TableBody>
    </Table>
  );
}

export default RootFolders;
