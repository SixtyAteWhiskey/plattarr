import React from 'react';
import { CustomFilter, Filter } from 'App/State/AppState';
import FilterMenu from 'Components/Menu/FilterMenu';
import IndexerFilterModal from './IndexerFilterModal';

interface IndexerFilterMenuProps {
  selectedFilterKey: string | number;
  filters: Filter[];
  customFilters: CustomFilter[];
  isDisabled: boolean;
  onFilterSelect: (filter: number | string) => void;
}

function IndexerFilterMenu(props: IndexerFilterMenuProps) {
  const {
    selectedFilterKey,
    filters,
    customFilters,
    isDisabled,
    onFilterSelect,
  } = props;

  return (
    <FilterMenu
      alignMenu="left"
      isDisabled={isDisabled}
      selectedFilterKey={selectedFilterKey}
      filters={filters}
      customFilters={customFilters}
      filterModalConnectorComponent={IndexerFilterModal}
      onFilterSelect={onFilterSelect}
    />
  );
}

export default IndexerFilterMenu;
