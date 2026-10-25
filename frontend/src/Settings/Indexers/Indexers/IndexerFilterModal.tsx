import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import { setIndexerFilter } from 'Store/Actions/indexerIndexActions';

interface IndexerFilterModalProps {
  isOpen: boolean;
}

export default function IndexerFilterModal(props: IndexerFilterModalProps) {
  const sectionItems = useSelector(
    (state: AppState) => state.settings.indexers.items
  );

  const filterBuilderProps = useSelector(
    (state: AppState) => state.indexerIndex.filterBuilderProps
  );

  const dispatch = useDispatch();

  const dispatchSetFilter = useCallback(
    (payload: unknown) => {
      dispatch(setIndexerFilter(payload));
    },
    [dispatch]
  );

  return (
    <FilterModal
      // TODO: Don't spread all the props
      {...props}
      sectionItems={sectionItems}
      filterBuilderProps={filterBuilderProps}
      customFilterType="indexerIndex"
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
