import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue from './FilterBuilderRowValue';
import FilterBuilderRowValueProps from './FilterBuilderRowValueProps';

const artistStatusList = [
  {
    id: 'continuing',
    get name() {
      return translate('Continuing');
    },
  },
  {
    id: 'ended',
    get name() {
      return translate('Inactive');
    },
  },
];

function ArtistStatusFilterBuilderRowValue(props: FilterBuilderRowValueProps) {
  return <FilterBuilderRowValue {...props} tagList={artistStatusList} />;
}

export default ArtistStatusFilterBuilderRowValue;
