import React from 'react';
import { CustomFilter, Filter } from 'App/State/AppState';
import ArtistIndexFilterModal from 'Artist/Index/ArtistIndexFilterModal';
import FilterMenu from 'Components/Menu/FilterMenu';

interface ArtistIndexFilterMenuProps {
  selectedFilterKey: string | number;
  filters: Filter[];
  customFilters: CustomFilter[];
  isDisabled: boolean;
  onFilterSelect: (filter: number | string) => void;
}

function ArtistIndexFilterMenu(props: ArtistIndexFilterMenuProps) {
  const {
    selectedFilterKey,
    filters,
    customFilters,
    isDisabled,
    onFilterSelect,
  } = props;

  return (
    <FilterMenu
      alignMenu="right"
      isDisabled={isDisabled}
      selectedFilterKey={selectedFilterKey}
      filters={filters}
      customFilters={customFilters}
      filterModalConnectorComponent={ArtistIndexFilterModal}
      onFilterSelect={onFilterSelect}
    />
  );
}

export default ArtistIndexFilterMenu;
