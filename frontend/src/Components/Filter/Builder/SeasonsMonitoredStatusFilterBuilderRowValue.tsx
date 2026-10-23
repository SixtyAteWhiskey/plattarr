import React from 'react';
import translate from 'Utilities/String/translate';
import FilterBuilderRowValue from './FilterBuilderRowValue';
import FilterBuilderRowValueProps from './FilterBuilderRowValueProps';

const seasonsMonitoredStatusList = [
  {
    id: 'all',
    get name() {
      return translate('SeasonsMonitoredAll');
    },
  },
  {
    id: 'partial',
    get name() {
      return translate('SeasonsMonitoredPartial');
    },
  },
  {
    id: 'none',
    get name() {
      return translate('SeasonsMonitoredNone');
    },
  },
];

function SeasonsMonitoredStatusFilterBuilderRowValue(
  props: FilterBuilderRowValueProps
) {
  return (
    <FilterBuilderRowValue {...props} tagList={seasonsMonitoredStatusList} />
  );
}

export default SeasonsMonitoredStatusFilterBuilderRowValue;
