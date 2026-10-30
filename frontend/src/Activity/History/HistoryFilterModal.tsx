import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setHistoryFilter } from 'Store/Actions/historyActions';

const HistoryFilterModal = createConnectedFilterModal({
  customFilterType: 'history',
  selectItems: (state) => state.history.items,
  selectFilterBuilderProps: (state) => state.history.filterBuilderProps,
  setFilter: setHistoryFilter,
});

export default HistoryFilterModal;
