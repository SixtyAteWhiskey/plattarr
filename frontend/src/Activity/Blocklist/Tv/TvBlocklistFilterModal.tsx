import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setTvBlocklistFilter } from 'Store/Actions/tvBlocklistActions';

const TvBlocklistFilterModal = createConnectedFilterModal({
  customFilterType: 'tvBlocklist',
  selectItems: (state) => state.tvBlocklist.items,
  selectFilterBuilderProps: (state) => state.tvBlocklist.filterBuilderProps,
  setFilter: setTvBlocklistFilter,
});

export default TvBlocklistFilterModal;
