import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setArtistFilter } from 'Store/Actions/artistIndexActions';

const ArtistIndexFilterModal = createConnectedFilterModal({
  customFilterType: 'artist',
  selectItems: (state) => state.artist.items,
  selectFilterBuilderProps: (state) => state.artistIndex.filterBuilderProps,
  setFilter: setArtistFilter,
});

export default ArtistIndexFilterModal;
