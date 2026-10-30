import React, { useCallback } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import FilterModal from './FilterModal';

interface ConnectedFilterModalProps {
  isOpen: boolean;
}

interface ConnectedFilterModalConfig<TItems, TBuilderProps> {
  customFilterType: string;
  selectItems: (state: AppState) => TItems;
  selectFilterBuilderProps: (state: AppState) => TBuilderProps;
  setFilter: (payload: unknown) => unknown;
}

export default function createConnectedFilterModal<TItems, TBuilderProps>({
  customFilterType,
  selectItems,
  selectFilterBuilderProps,
  setFilter,
}: ConnectedFilterModalConfig<TItems, TBuilderProps>) {
  return function ConnectedFilterModal(props: ConnectedFilterModalProps) {
    const sectionItems = useSelector(selectItems);
    const filterBuilderProps = useSelector(selectFilterBuilderProps);
    const dispatch = useDispatch();

    const dispatchSetFilter = useCallback(
      (payload: unknown) => {
        dispatch(setFilter(payload));
      },
      [dispatch]
    );

    return (
      <FilterModal
        {...props}
        sectionItems={sectionItems}
        filterBuilderProps={filterBuilderProps}
        customFilterType={customFilterType}
        dispatchSetFilter={dispatchSetFilter}
      />
    );
  };
}
