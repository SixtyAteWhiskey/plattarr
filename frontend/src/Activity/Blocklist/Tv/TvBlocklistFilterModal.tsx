import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import { setTvBlocklistFilter } from 'Store/Actions/tvBlocklistActions';

function createBlocklistSelector() {
  return createSelector(
    (state: AppState) => state.tvBlocklist.items,
    (blocklistItems) => {
      return blocklistItems;
    }
  );
}

function createFilterBuilderPropsSelector() {
  return createSelector(
    (state: AppState) => state.tvBlocklist.filterBuilderProps,
    (filterBuilderProps) => {
      return filterBuilderProps;
    }
  );
}

interface TvBlocklistFilterModalProps {
  isOpen: boolean;
}

export default function TvBlocklistFilterModal(
  props: TvBlocklistFilterModalProps
) {
  const sectionItems = useSelector(createBlocklistSelector());
  const filterBuilderProps = useSelector(createFilterBuilderPropsSelector());
  const customFilterType = 'tvBlocklist';

  const dispatch = useDispatch();

  const dispatchSetFilter = useCallback(
    (payload: unknown) => {
      dispatch(setTvBlocklistFilter(payload));
    },
    [dispatch]
  );

  return (
    <FilterModal
      // TODO: Don't spread all the props
      {...props}
      sectionItems={sectionItems}
      filterBuilderProps={filterBuilderProps}
      customFilterType={customFilterType}
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
