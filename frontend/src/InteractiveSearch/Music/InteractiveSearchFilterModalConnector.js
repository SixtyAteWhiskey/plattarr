import { connect } from 'react-redux';
import { createSelector } from 'reselect';
import FilterModal from 'Components/Filter/FilterModal';
import { setAlbumReleasesFilter, setArtistReleasesFilter } from 'Store/Actions/musicReleaseActions';

function createMapStateToProps() {
  return createSelector(
    (state) => state.musicReleases.items,
    (state) => state.musicReleases.filterBuilderProps,
    (sectionItems, filterBuilderProps) => {
      return {
        sectionItems,
        filterBuilderProps,
        customFilterType: 'musicReleases'
      };
    }
  );
}

function createMapDispatchToProps(dispatch, props) {
  return {
    dispatchSetFilter(payload) {
      const action = props.type === 'album' ?
        setAlbumReleasesFilter:
        setArtistReleasesFilter;

      dispatch(action(payload));
    }
  };
}

export default connect(createMapStateToProps, createMapDispatchToProps)(FilterModal);
