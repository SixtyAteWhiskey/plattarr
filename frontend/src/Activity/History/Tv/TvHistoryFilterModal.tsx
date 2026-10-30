import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setTvHistoryFilter } from 'Store/Actions/tvHistoryActions';

const TvHistoryFilterModal = createConnectedFilterModal({
  customFilterType: 'tvHistory',
  selectItems: (state) => state.tvHistory.items,
  selectFilterBuilderProps: (state) => state.tvHistory.filterBuilderProps,
  setFilter: setTvHistoryFilter,
});

export default TvHistoryFilterModal;
