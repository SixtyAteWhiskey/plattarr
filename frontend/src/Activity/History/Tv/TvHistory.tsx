import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import MediaKindSwitcher from 'Components/MediaKindSwitcher';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import PageToolbar from 'Components/Page/Toolbar/PageToolbar';
import PageToolbarButton from 'Components/Page/Toolbar/PageToolbarButton';
import PageToolbarSection from 'Components/Page/Toolbar/PageToolbarSection';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableOptionsModalWrapper from 'Components/Table/TableOptions/TableOptionsModalWrapper';
import TablePager from 'Components/Table/TablePager';
import usePaging from 'Components/Table/usePaging';
import createEpisodesFetchingSelector from 'Episode/createEpisodesFetchingSelector';
import useCurrentPage from 'Helpers/Hooks/useCurrentPage';
import { align, icons, kinds } from 'Helpers/Props';
import { clearEpisodes, fetchEpisodes } from 'Store/Actions/episodeActions';
import { clearEpisodeFiles } from 'Store/Actions/episodeFileActions';
import {
  clearTvHistory,
  fetchTvHistory,
  gotoTvHistoryPage,
  setTvHistoryFilter,
  setTvHistorySort,
  setTvHistoryTableOption,
} from 'Store/Actions/tvHistoryActions';
import { createCustomFiltersSelector } from 'Store/Selectors/createClientSideCollectionSelector';
import { SeriesHistory } from 'typings/History';
import { TableOptionsChangePayload } from 'typings/Table';
import selectUniqueIds from 'Utilities/Object/selectUniqueIds';
import {
  registerPagePopulator,
  unregisterPagePopulator,
} from 'Utilities/pagePopulator';
import translate from 'Utilities/String/translate';
import HistoryPageContent from '../HistoryPageContent';
import TvHistoryFilterModal from './TvHistoryFilterModal';
import TvHistoryRow from './TvHistoryRow';

function TvHistory() {
  const requestCurrentPage = useCurrentPage();

  const {
    isFetching,
    isPopulated,
    error,
    items,
    columns,
    selectedFilterKey,
    filters,
    sortKey,
    sortDirection,
    page,
    pageSize,
    totalPages,
    totalRecords,
  } = useSelector((state: AppState) => state.tvHistory);

  const { isEpisodesFetching, isEpisodesPopulated, episodesError } =
    useSelector(createEpisodesFetchingSelector());
  const customFilters = useSelector(createCustomFiltersSelector('tvHistory'));
  const dispatch = useDispatch();

  const isFetchingAny = isFetching || isEpisodesFetching;
  const isAllPopulated = isPopulated && (isEpisodesPopulated || !items.length);
  const hasError = error || episodesError;

  const {
    handleFirstPagePress,
    handlePreviousPagePress,
    handleNextPagePress,
    handleLastPagePress,
    handlePageSelect,
  } = usePaging({
    page,
    totalPages,
    gotoPage: gotoTvHistoryPage,
  });

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      dispatch(setTvHistoryFilter({ selectedFilterKey }));
    },
    [dispatch]
  );

  const handleSortPress = useCallback(
    (sortKey: string) => {
      dispatch(setTvHistorySort({ sortKey }));
    },
    [dispatch]
  );

  const handleTableOptionChange = useCallback(
    (payload: TableOptionsChangePayload) => {
      dispatch(setTvHistoryTableOption(payload));

      if (payload.pageSize) {
        dispatch(gotoTvHistoryPage({ page: 1 }));
      }
    },
    [dispatch]
  );

  useEffect(() => {
    if (requestCurrentPage) {
      dispatch(fetchTvHistory());
    } else {
      dispatch(gotoTvHistoryPage({ page: 1 }));
    }

    return () => {
      dispatch(clearTvHistory());
      dispatch(clearEpisodes());
      dispatch(clearEpisodeFiles());
    };
  }, [requestCurrentPage, dispatch]);

  useEffect(() => {
    const episodeIds = selectUniqueIds<SeriesHistory, number>(
      items,
      'episodeId'
    );

    if (episodeIds.length) {
      dispatch(fetchEpisodes({ episodeIds }));
    } else {
      dispatch(clearEpisodes());
    }
  }, [items, dispatch]);

  useEffect(() => {
    const repopulate = () => {
      dispatch(fetchTvHistory());
    };

    registerPagePopulator(repopulate);

    return () => {
      unregisterPagePopulator(repopulate);
    };
  }, [dispatch]);

  return (
    <PageContent title={translate('History')}>
      <PageToolbar>
        <PageToolbarSection>
          <MediaKindSwitcher />

          <PageToolbarButton
            label={translate('Refresh')}
            iconName={icons.REFRESH}
            isSpinning={isFetching}
            onPress={handleFirstPagePress}
          />
        </PageToolbarSection>

        <PageToolbarSection alignContent={align.RIGHT}>
          <TableOptionsModalWrapper
            columns={columns}
            pageSize={pageSize}
            onTableOptionChange={handleTableOptionChange}
          >
            <PageToolbarButton
              label={translate('Options')}
              iconName={icons.TABLE}
            />
          </TableOptionsModalWrapper>

          <FilterMenu
            alignMenu={align.RIGHT}
            selectedFilterKey={selectedFilterKey}
            filters={filters}
            customFilters={customFilters}
            filterModalConnectorComponent={TvHistoryFilterModal}
            onFilterSelect={handleFilterSelect}
          />
        </PageToolbarSection>
      </PageToolbar>

      <PageContentBody>
        <HistoryPageContent>
          {isFetchingAny && !isAllPopulated ? <LoadingIndicator /> : null}

          {!isFetchingAny && hasError ? (
            <Alert role="alert" kind={kinds.DANGER}>
              {translate('HistoryLoadError')}
            </Alert>
          ) : null}

          {
            // If history isPopulated and it's empty show no history found and don't
            // wait for the episodes to populate because they are never coming.

            isPopulated && !hasError && !items.length ? (
              <Alert kind={kinds.INFO}>{translate('NoHistoryFound')}</Alert>
            ) : null
          }

          {isAllPopulated && !hasError && items.length ? (
            <div>
              <Table
                columns={columns}
                pageSize={pageSize}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onTableOptionChange={handleTableOptionChange}
                onSortPress={handleSortPress}
              >
                <TableBody>
                  {items.map((item) => {
                    return (
                      <TvHistoryRow key={item.id} columns={columns} {...item} />
                    );
                  })}
                </TableBody>
              </Table>

              <TablePager
                page={page}
                totalPages={totalPages}
                totalRecords={totalRecords}
                isFetching={isFetching}
                onFirstPagePress={handleFirstPagePress}
                onPreviousPagePress={handlePreviousPagePress}
                onNextPagePress={handleNextPagePress}
                onLastPagePress={handleLastPagePress}
                onPageSelect={handlePageSelect}
              />
            </div>
          ) : null}
        </HistoryPageContent>
      </PageContentBody>
    </PageContent>
  );
}

export default TvHistory;
