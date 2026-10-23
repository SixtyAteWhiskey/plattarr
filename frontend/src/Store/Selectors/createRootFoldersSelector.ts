import { createSelector } from 'reselect';
import RootFolderAppState from 'App/State/RootFolderAppState';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import RootFolder from 'typings/RootFolder';
import sortByProp from 'Utilities/Array/sortByProp';

export default function createRootFoldersSelector(
  section: 'rootFolders' | 'tvRootFolders' = 'rootFolders'
) {
  return createSelector(
    createSortedSectionSelector<RootFolder, RootFolderAppState>(
      section,
      sortByProp('path')
    ),
    (rootFolders: RootFolderAppState) => rootFolders
  );
}
