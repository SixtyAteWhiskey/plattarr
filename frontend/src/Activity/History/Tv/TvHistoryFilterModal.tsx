import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import { setTvHistoryFilter } from 'Store/Actions/tvHistoryActions';

function createTvHistorySelector() {
  return createSelector(
    (state: AppState) => state.tvHistory.items,
    (queueItems) => {
      return queueItems;
    }
  );
}

function createFilterBuilderPropsSelector() {
  return createSelector(
    (state: AppState) => state.tvHistory.filterBuilderProps,
    (filterBuilderProps) => {
      return filterBuilderProps;
    }
  );
}

interface TvHistoryFilterModalProps {
  isOpen: boolean;
}

export default function TvHistoryFilterModal(props: TvHistoryFilterModalProps) {
  const sectionItems = useSelector(createTvHistorySelector());
  const filterBuilderProps = useSelector(createFilterBuilderPropsSelector());
  const customFilterType = 'tvHistory';

  const dispatch = useDispatch();

  const dispatchSetFilter = useCallback(
    (payload: unknown) => {
      dispatch(setTvHistoryFilter(payload));
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
