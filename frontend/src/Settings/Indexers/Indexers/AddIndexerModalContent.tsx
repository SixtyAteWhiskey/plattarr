import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import EnhancedSelectInput from 'Components/Form/Select/EnhancedSelectInput';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { kinds, sortDirections } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import { fetchIndexerSchema } from 'Store/Actions/settingsActions';
import Indexer from 'typings/Indexer';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import AddIndexerRow from './AddIndexerRow';
import getIndexerCategories from './getIndexerCategories';
import getIndexerSchemaIdentity from './getIndexerSchemaIdentity';
import styles from './AddIndexerModalContent.css';

interface AddIndexerModalContentProps {
  onIndexerSelect: () => void;
  onModalClose: () => void;
}

type SortKey = 'protocol' | 'name' | 'language' | 'privacy';

const columns = [
  {
    name: 'protocol',
    label: () => translate('Protocol'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'name',
    label: () => translate('Name'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'language',
    label: () => translate('Language'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'description',
    label: () => translate('Description'),
    isSortable: false,
    isVisible: true,
  },
  {
    name: 'privacy',
    label: () => translate('Privacy'),
    isSortable: true,
    isVisible: true,
  },
  {
    name: 'categories',
    label: () => translate('Categories'),
    isSortable: false,
    isVisible: true,
  },
];

function getSortValue(indexer: Indexer, sortKey: SortKey) {
  return (indexer[sortKey] ?? '').toString();
}

function AddIndexerModalContent({
  onIndexerSelect,
  onModalClose,
}: AddIndexerModalContentProps) {
  const dispatch = useDispatch();

  const {
    isSchemaFetching,
    isSchemaPopulated,
    schemaError,
    schema,
    items: addedIndexers,
  } = useSelector((state: AppState) => state.settings.indexers);

  const addedIndexerIdentities = useMemo(() => {
    return new Set(
      addedIndexers
        .map(getIndexerSchemaIdentity)
        .filter((identity): identity is string => identity !== null)
    );
  }, [addedIndexers]);

  // Built here, not at module level: translations load after the bundle.
  const protocolValues = useMemo(
    () => [
      { key: 'torrent', value: translate('Torrents') },
      { key: 'usenet', value: translate('Usenet') },
    ],
    []
  );

  const privacyValues = useMemo(
    () => [
      { key: 'public', value: translate('Public') },
      { key: 'semiPrivate', value: translate('SemiPrivate') },
      { key: 'private', value: translate('Private') },
    ],
    []
  );

  const [filter, setFilter] = useState('');
  const [protocols, setProtocols] = useState<string[]>([]);
  const [languages, setLanguages] = useState<string[]>([]);
  const [privacies, setPrivacies] = useState<string[]>([]);
  const [categories, setCategories] = useState<number[]>([]);
  const [sortKey, setSortKey] = useState<SortKey>('name');
  const [sortDirection, setSortDirection] = useState<SortDirection>(
    sortDirections.ASCENDING
  );

  const handleFilterChange = useCallback(({ value }: InputChanged<string>) => {
    setFilter(value);
  }, []);

  const handleProtocolsChange = useCallback(
    ({ value }: { value: string[] }) => setProtocols(value),
    []
  );

  const handleLanguagesChange = useCallback(
    ({ value }: { value: string[] }) => setLanguages(value),
    []
  );

  const handlePrivaciesChange = useCallback(
    ({ value }: { value: string[] }) => setPrivacies(value),
    []
  );

  const handleCategoriesChange = useCallback(
    ({ value }: { value: number[] }) => setCategories(value),
    []
  );

  const handleSortPress = useCallback(
    (name: string, direction?: SortDirection) => {
      setSortKey(name as SortKey);
      setSortDirection(direction ?? sortDirections.ASCENDING);
    },
    []
  );

  // The schema holds every indexer definition (hundreds), so the dialog
  // offers a search box plus protocol, language, privacy and category
  // filters, as Prowlarr does.
  const languageValues = useMemo(() => {
    const names = new Set<string>();

    schema.forEach((item) => {
      if (item.language) {
        names.add(item.language);
      }
    });

    return [...names].sort().map((name) => ({ key: name, value: name }));
  }, [schema]);

  const categoryValues = useMemo(() => {
    const names = new Map<number, string>();

    schema.forEach((item) => {
      getIndexerCategories(item).forEach((category) => {
        names.set(category.id, category.name);
      });
    });

    return [...names.entries()]
      .sort(([a], [b]) => a - b)
      .map(([id, name]) => ({ key: id, value: name }));
  }, [schema]);

  const items = useMemo(() => {
    const filterLower = filter.trim().toLowerCase();

    const filtered = schema.filter((item) => {
      if (
        filterLower &&
        !item.name.toLowerCase().includes(filterLower) &&
        !(item.description ?? '').toLowerCase().includes(filterLower)
      ) {
        return false;
      }

      if (protocols.length && !protocols.includes(item.protocol)) {
        return false;
      }

      if (languages.length && !languages.includes(item.language ?? '')) {
        return false;
      }

      if (privacies.length && !privacies.includes(item.privacy ?? '')) {
        return false;
      }

      if (
        categories.length &&
        !getIndexerCategories(item).some(({ id }) => categories.includes(id))
      ) {
        return false;
      }

      return true;
    });

    const direction = sortDirection === sortDirections.ASCENDING ? 1 : -1;

    return filtered.sort((a, b) => {
      const result = getSortValue(a, sortKey).localeCompare(
        getSortValue(b, sortKey),
        undefined,
        { sensitivity: 'base' }
      );

      return (
        (result ||
          a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })) *
        direction
      );
    });
  }, [
    schema,
    filter,
    protocols,
    languages,
    privacies,
    categories,
    sortKey,
    sortDirection,
  ]);

  useEffect(() => {
    dispatch(fetchIndexerSchema());
  }, [dispatch]);

  return (
    <ModalContent onModalClose={onModalClose}>
      <ModalHeader>{translate('AddIndexer')}</ModalHeader>

      <ModalBody>
        {isSchemaFetching ? <LoadingIndicator /> : null}

        {!isSchemaFetching && !!schemaError ? (
          <Alert kind={kinds.DANGER}>{translate('AddIndexerError')}</Alert>
        ) : null}

        {isSchemaPopulated && !schemaError ? (
          <div>
            <TextInput
              className={styles.filterInput}
              name="filter"
              value={filter}
              placeholder={translate('SearchIndexers')}
              autoFocus={true}
              onChange={handleFilterChange}
            />

            <div className={styles.filters}>
              <div className={styles.filter}>
                <div className={styles.filterLabel}>
                  {translate('Protocol')}
                </div>
                <EnhancedSelectInput
                  name="protocols"
                  value={protocols}
                  values={protocolValues}
                  onChange={handleProtocolsChange}
                />
              </div>

              <div className={styles.filter}>
                <div className={styles.filterLabel}>
                  {translate('Language')}
                </div>
                <EnhancedSelectInput
                  name="languages"
                  value={languages}
                  values={languageValues}
                  onChange={handleLanguagesChange}
                />
              </div>

              <div className={styles.filter}>
                <div className={styles.filterLabel}>{translate('Privacy')}</div>
                <EnhancedSelectInput
                  name="privacies"
                  value={privacies}
                  values={privacyValues}
                  onChange={handlePrivaciesChange}
                />
              </div>

              <div className={styles.filter}>
                <div className={styles.filterLabel}>
                  {translate('Categories')}
                </div>
                <EnhancedSelectInput
                  name="categories"
                  value={categories}
                  values={categoryValues}
                  onChange={handleCategoriesChange}
                />
              </div>
            </div>

            <Alert kind={kinds.INFO}>
              <div>{translate('SupportedIndexers')}</div>
              <div>{translate('SupportedIndexersMoreInfo')}</div>
            </Alert>

            {items.length === 0 ? (
              <Alert kind={kinds.WARNING}>{translate('NoIndexersFound')}</Alert>
            ) : (
              <Table
                columns={columns}
                sortKey={sortKey}
                sortDirection={sortDirection}
                onSortPress={handleSortPress}
              >
                <TableBody>
                  {items.map((indexer) => (
                    <AddIndexerRow
                      key={`${indexer.implementation}:${indexer.name}`}
                      indexer={indexer}
                      isAdded={addedIndexerIdentities.has(
                        getIndexerSchemaIdentity(indexer) ?? ''
                      )}
                      onIndexerSelect={onIndexerSelect}
                    />
                  ))}
                </TableBody>
              </Table>
            )}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        {isSchemaPopulated && !schemaError ? (
          <div className={styles.count}>
            {translate('IndexersAvailable', { count: items.length })}
          </div>
        ) : null}

        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default AddIndexerModalContent;
