import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setBlocklistFilter } from 'Store/Actions/blocklistActions';

const BlocklistFilterModal = createConnectedFilterModal({
  customFilterType: 'blocklist',
  selectItems: (state) => state.blocklist.items,
  selectFilterBuilderProps: (state) => state.blocklist.filterBuilderProps,
  setFilter: setBlocklistFilter,
});

export default BlocklistFilterModal;
