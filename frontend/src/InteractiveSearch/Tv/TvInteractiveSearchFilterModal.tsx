import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import AppState from 'App/State/AppState';
import FilterModal from 'Components/Filter/FilterModal';
import {
  setEpisodeReleasesFilter,
  setSeasonReleasesFilter,
} from 'Store/Actions/tvReleaseActions';
import TvInteractiveSearchType from './TvInteractiveSearchType';

function createReleasesSelector() {
  return createSelector(
    (state: AppState) => state.tvReleases.items,
    (releases) => {
      return releases;
    }
  );
}

function createFilterBuilderPropsSelector() {
  return createSelector(
    (state: AppState) => state.tvReleases.filterBuilderProps,
    (filterBuilderProps) => {
      return filterBuilderProps;
    }
  );
}

interface TvInteractiveSearchFilterModalProps {
  isOpen: boolean;
  type: TvInteractiveSearchType;
}

export default function TvInteractiveSearchFilterModal({
  type,
  ...otherProps
}: TvInteractiveSearchFilterModalProps) {
  const sectionItems = useSelector(createReleasesSelector());
  const filterBuilderProps = useSelector(createFilterBuilderPropsSelector());

  const dispatch = useDispatch();

  const dispatchSetFilter = useCallback(
    (payload: unknown) => {
      const action =
        type === 'episode' ? setEpisodeReleasesFilter : setSeasonReleasesFilter;

      dispatch(action(payload));
    },
    [type, dispatch]
  );

  return (
    <FilterModal
      // TODO: Don't spread all the props
      {...otherProps}
      sectionItems={sectionItems}
      filterBuilderProps={filterBuilderProps}
      customFilterType="tvReleases"
      dispatchSetFilter={dispatchSetFilter}
    />
  );
}
