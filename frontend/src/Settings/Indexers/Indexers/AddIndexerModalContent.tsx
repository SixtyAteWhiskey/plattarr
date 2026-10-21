import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import TextInput from 'Components/Form/TextInput';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds } from 'Helpers/Props';
import { fetchIndexerSchema } from 'Store/Actions/settingsActions';
import Indexer from 'typings/Indexer';
import { InputChanged } from 'typings/inputs';
import translate from 'Utilities/String/translate';
import AddIndexerItem from './AddIndexerItem';
import styles from './AddIndexerModalContent.css';

interface AddIndexerModalContentProps {
  onIndexerSelect: () => void;
  onModalClose: () => void;
}

function AddIndexerModalContent({
  onIndexerSelect,
  onModalClose,
}: AddIndexerModalContentProps) {
  const dispatch = useDispatch();

  const { isSchemaFetching, isSchemaPopulated, schemaError, schema } =
    useSelector((state: AppState) => state.settings.indexers);

  const [filter, setFilter] = useState('');

  const handleFilterChange = useCallback(({ value }: InputChanged<string>) => {
    setFilter(value);
  }, []);

  // Plattarr: the schema holds every Prowlarr indexer definition (hundreds),
  // so the list is sorted by name and filterable.
  const { usenetIndexers, torrentIndexers } = useMemo(() => {
    const filterLower = filter.trim().toLowerCase();

    const sorted = [...schema].sort((a, b) =>
      a.name.localeCompare(b.name, undefined, { sensitivity: 'base' })
    );

    return sorted.reduce<{
      usenetIndexers: Indexer[];
      torrentIndexers: Indexer[];
    }>(
      (acc, item) => {
        if (filterLower && !item.name.toLowerCase().includes(filterLower)) {
          return acc;
        }

        if (item.protocol === 'usenet') {
          acc.usenetIndexers.push(item);
        } else if (item.protocol === 'torrent') {
          acc.torrentIndexers.push(item);
        }

        return acc;
      },
      {
        usenetIndexers: [],
        torrentIndexers: [],
      }
    );
  }, [schema, filter]);

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
            <Alert kind={kinds.INFO}>
              <div>{translate('SupportedIndexers')}</div>
              <div>{translate('SupportedIndexersMoreInfo')}</div>
            </Alert>

            <TextInput
              className={styles.filterInput}
              name="filter"
              value={filter}
              placeholder={translate('Filter')}
              autoFocus={true}
              onChange={handleFilterChange}
            />

            {usenetIndexers.length === 0 && torrentIndexers.length === 0 ? (
              <Alert kind={kinds.WARNING}>{translate('NoIndexersFound')}</Alert>
            ) : null}

            {usenetIndexers.length ? (
              <FieldSet legend={translate('Usenet')}>
                <div className={styles.indexers}>
                  {usenetIndexers.map((indexer) => {
                    return (
                      <AddIndexerItem
                        key={`${indexer.implementation}:${indexer.name}`}
                        {...indexer}
                        implementation={indexer.implementation}
                        schemaName={indexer.name}
                        onIndexerSelect={onIndexerSelect}
                      />
                    );
                  })}
                </div>
              </FieldSet>
            ) : null}

            {torrentIndexers.length ? (
              <FieldSet legend={translate('Torrents')}>
                <div className={styles.indexers}>
                  {torrentIndexers.map((indexer) => {
                    return (
                      <AddIndexerItem
                        key={`${indexer.implementation}:${indexer.name}`}
                        {...indexer}
                        implementation={indexer.implementation}
                        schemaName={indexer.name}
                        onIndexerSelect={onIndexerSelect}
                      />
                    );
                  })}
                </div>
              </FieldSet>
            ) : null}
          </div>
        ) : null}
      </ModalBody>

      <ModalFooter>
        <Button onPress={onModalClose}>{translate('Close')}</Button>
      </ModalFooter>
    </ModalContent>
  );
}

export default AddIndexerModalContent;
