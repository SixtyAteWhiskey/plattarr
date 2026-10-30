import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setSeriesFilter } from 'Store/Actions/seriesIndexActions';

const SeriesIndexFilterModal = createConnectedFilterModal({
  customFilterType: 'series',
  selectItems: (state) => state.series.items,
  selectFilterBuilderProps: (state) => state.seriesIndex.filterBuilderProps,
  setFilter: setSeriesFilter,
});

export default SeriesIndexFilterModal;
