import React from 'react';
import AlbumDetails from 'Artist/Index/Select/AlbumStudio/AlbumDetails';
import VirtualTableRowCell from 'Components/Table/Cells/VirtualTableRowCell';
import Popover from 'Components/Tooltip/Popover';
import { LEFT } from 'Helpers/Props/tooltipPositions';
import translate from 'Utilities/String/translate';
import styles from './AlbumsCell.css';

interface SeriesStatusCellProps {
  className: string;
  artistId: number;
  albumCount: number;
  isSelectMode: boolean;
}

function AlbumsCell(props: SeriesStatusCellProps) {
  const { className, artistId, albumCount, isSelectMode, ...otherProps } =
    props;

  return (
    <VirtualTableRowCell className={className} {...otherProps}>
      {isSelectMode && albumCount > 0 ? (
        <Popover
          className={styles.albumCount}
          anchor={albumCount}
          title={translate('AlbumDetails')}
          body={<AlbumDetails artistId={artistId} />}
          position={LEFT}
          canFlip={true}
        />
      ) : (
        albumCount
      )}
    </VirtualTableRowCell>
  );
}

export default AlbumsCell;
