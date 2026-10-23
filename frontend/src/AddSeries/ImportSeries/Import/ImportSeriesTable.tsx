import React, { useCallback, useEffect, useRef } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import { GridCellProps } from 'react-virtualized';
import { useAddSeriesOptions } from 'AddSeries/addSeriesOptionsStore';
import ModelBase from 'App/ModelBase';
import { useSelect } from 'App/SelectContext';
import AppState from 'App/State/AppState';
import VirtualTable from 'Components/Table/VirtualTable';
import VirtualTableRow from 'Components/Table/VirtualTableRow';
import usePrevious from 'Helpers/Hooks/usePrevious';
import {
  queueLookupSeries,
  setImportSeriesValue,
} from 'Store/Actions/importSeriesActions';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import createDimensionsSelector from 'Store/Selectors/createDimensionsSelector';
import { CheckInputChanged } from 'typings/inputs';
import { SelectStateInputProps } from 'typings/props';
import { UnmappedFolder } from 'typings/RootFolder';
import ImportSeriesHeader from './ImportSeriesHeader';
import ImportSeriesRow from './ImportSeriesRow';

const ROW_HEIGHT = 52;

interface ImportSeriesTableProps {
  unmappedFolders: UnmappedFolder[];
  scroller: Element;
}

function ImportSeriesTable({
  unmappedFolders,
  scroller,
}: ImportSeriesTableProps) {
  const dispatch = useDispatch();

  const { monitor, qualityProfileId, seriesType, seasonFolder } =
    useAddSeriesOptions();

  const items = useSelector((state: AppState) => state.importSeries.items);
  const { isSmallScreen } = useSelector(createDimensionsSelector());
  const allSeries = useSelector(createAllSeriesSelector());
  const [selectState, selectDispatch] = useSelect();

  const defaultValues = useRef({
    monitor,
    qualityProfileId,
    seriesType,
    seasonFolder,
  });

  const initialUnmappedFolders = useRef(unmappedFolders);
  const previousItems = usePrevious(items);
  const { allSelected, allUnselected, selectedState } = selectState;

  const handleSelectAllChange = useCallback(
    ({ value }: CheckInputChanged) => {
      selectDispatch({
        type: value ? 'selectAll' : 'unselectAll',
      });
    },
    [selectDispatch]
  );

  const handleSelectedChange = useCallback(
    ({ id, value, shiftKey }: SelectStateInputProps) => {
      selectDispatch({
        type: 'toggleSelected',
        id,
        isSelected: value,
        shiftKey,
      });
    },
    [selectDispatch]
  );

  const handleRemoveSelectedStateItem = useCallback(
    (id: string) => {
      selectDispatch({
        type: 'removeItem',
        id,
      });
    },
    [selectDispatch]
  );

  useEffect(() => {
    initialUnmappedFolders.current.forEach(({ name, path, relativePath }) => {
      dispatch(
        queueLookupSeries({
          name,
          path,
          relativePath,
          term: name,
        })
      );

      dispatch(
        // @ts-expect-error - actions are not typed
        setImportSeriesValue({
          id: name,
          ...defaultValues.current,
        })
      );
    });
  }, [dispatch]);

  useEffect(() => {
    previousItems?.forEach((prevItem) => {
      const { id } = prevItem;

      const item = items.find((i) => i.id === id);

      if (!item) {
        handleRemoveSelectedStateItem(id);
        return;
      }

      const selectedSeries = item.selectedSeries;
      const isSelected = selectedState[id];

      const isExistingSeries =
        !!selectedSeries &&
        allSeries.some((s) => s.tvdbId === selectedSeries.tvdbId);

      if (
        (!selectedSeries && prevItem.selectedSeries) ||
        (isExistingSeries && !prevItem.selectedSeries)
      ) {
        handleSelectedChange({ id, value: false, shiftKey: false });

        return;
      }

      if (isSelected && (!selectedSeries || isExistingSeries)) {
        handleSelectedChange({ id, value: false, shiftKey: false });

        return;
      }

      if (selectedSeries && selectedSeries !== prevItem.selectedSeries) {
        handleSelectedChange({ id, value: true, shiftKey: false });

        return;
      }
    });
  }, [
    allSeries,
    items,
    previousItems,
    selectedState,
    handleRemoveSelectedStateItem,
    handleSelectedChange,
  ]);

  const rowRenderer = useCallback(
    ({ key, rowIndex, style }: GridCellProps) => {
      const item = items[rowIndex];

      if (!item) {
        return null;
      }

      return (
        <VirtualTableRow key={key} style={style}>
          <ImportSeriesRow id={item.id} />
        </VirtualTableRow>
      );
    },
    [items]
  );

  if (!items.length) {
    return null;
  }

  return (
    <VirtualTable
      items={items as unknown as ModelBase[]}
      isSmallScreen={isSmallScreen}
      scroller={scroller}
      rowHeight={ROW_HEIGHT}
      rowRenderer={rowRenderer}
      header={
        <ImportSeriesHeader
          allSelected={allSelected}
          allUnselected={allUnselected}
          onSelectAllChange={handleSelectAllChange}
        />
      }
      selectedState={selectedState}
    />
  );
}

export default ImportSeriesTable;
