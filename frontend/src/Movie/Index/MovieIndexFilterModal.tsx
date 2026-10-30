import createConnectedFilterModal from 'Components/Filter/createConnectedFilterModal';
import { setMovieFilter } from 'Store/Actions/movieIndexActions';

const MovieIndexFilterModal = createConnectedFilterModal({
  customFilterType: 'movieIndex',
  selectItems: (state) => state.movies.items,
  selectFilterBuilderProps: (state) => state.movieIndex.filterBuilderProps,
  setFilter: setMovieFilter,
});

export default MovieIndexFilterModal;
