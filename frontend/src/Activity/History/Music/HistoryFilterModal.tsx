import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setHistoryFilter } from 'Store/Actions/musicHistoryActions';

const HistoryFilterModal = createConnectedFilterModal({
  customFilterType: 'musicHistory',
  selectItems: (state) => state.musicHistory.items,
  selectFilterBuilderProps: (state) => state.musicHistory.filterBuilderProps,
  setFilter: setHistoryFilter,
});

export default HistoryFilterModal;
