import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue from './FilterBuilderRowValue';
import FilterBuilderRowValueProps from './FilterBuilderRowValueProps';

const seriesTypeList = [
  {
    id: 'anime',
    get name() {
      return translate('Anime');
    },
  },
  {
    id: 'daily',
    get name() {
      return translate('Daily');
    },
  },
  {
    id: 'standard',
    get name() {
      return translate('Standard');
    },
  },
];

function SeriesTypeFilterBuilderRowValue(props: FilterBuilderRowValueProps) {
  return <FilterBuilderRowValue {...props} tagList={seriesTypeList} />;
}

export default SeriesTypeFilterBuilderRowValue;
