import React, { useCallback, useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import { IconName } from 'Components/Icon';
import { Cluster } from 'Components/Layout';
import IconButton from 'Components/Link/IconButton';
import SpinnerIconButton from 'Components/Link/SpinnerIconButton';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import InlineMarkdown from 'Components/Markdown/InlineMarkdown';
import { StatusBadge, StatusTone } from 'Components/Status';
import Surface from 'Components/Surface';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import TableRow from 'Components/Table/TableRow';
import { icons, kinds } from 'Helpers/Props';
import {
  testAllDownloadClients,
  testAllIndexers,
} from 'Store/Actions/settingsActions';
import { fetchHealth } from 'Store/Actions/systemActions';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';
import createHealthSelector from './createHealthSelector';
import HealthItemLink from './HealthItemLink';
import styles from './Health.css';

const columns: Column[] = [
  {
    className: styles.status,
    name: 'type',
    label: () => translate('Status'),
    isVisible: true,
  },
  {
    name: 'message',
    label: () => translate('Message'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: () => translate('Actions'),
    isVisible: true,
  },
];

interface HealthPresentation {
  icon: IconName;
  label: string;
  tone: StatusTone;
}

function getHealthPresentation(type: string): HealthPresentation {
  switch (type.toLowerCase()) {
    case 'error':
      return {
        icon: icons.DANGER,
        label: translate('Error'),
        tone: 'danger',
      };
    case 'warning':
      return {
        icon: icons.WARNING,
        label: translate('Warning'),
        tone: 'warning',
      };
    case 'notice':
      return {
        icon: icons.INFO,
        label: titleCase(type),
        tone: 'info',
      };
    default:
      return {
        icon: icons.UNKNOWN,
        label: titleCase(type || translate('Unknown')),
        tone: 'neutral',
      };
  }
}

function Health() {
  const dispatch = useDispatch();
  const { error, isFetching, isPopulated, items } = useSelector(
    createHealthSelector()
  );
  const isTestingAllDownloadClients = useSelector(
    (state: AppState) => state.settings.downloadClients.isTestingAll
  );
  const isTestingAllIndexers = useSelector(
    (state: AppState) => state.settings.indexers.isTestingAll
  );

  const healthIssues = !!items.length;

  const handleTestAllDownloadClientsPress = useCallback(() => {
    dispatch(testAllDownloadClients());
  }, [dispatch]);

  const handleTestAllIndexersPress = useCallback(() => {
    dispatch(testAllIndexers());
  }, [dispatch]);

  useEffect(() => {
    dispatch(fetchHealth());
  }, [dispatch]);

  return (
    <FieldSet
      legend={
        <div className={styles.legend}>
          {translate('Health')}

          {isFetching && isPopulated ? (
            <LoadingIndicator className={styles.loading} size={20} />
          ) : null}
        </div>
      }
    >
      {isFetching && !isPopulated ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <Alert kind={kinds.DANGER} role="alert">
          {translate('ErrorLoadingContent')}
        </Alert>
      ) : null}

      {isPopulated && !error && !healthIssues ? (
        <Surface
          className={styles.healthOk}
          padding="small"
          tone="success"
          variant="tinted"
        >
          <Cluster gap="small">
            <StatusBadge
              icon={icons.CHECK_CIRCLE}
              label={translate('Ok')}
              tone="success"
            />
            <span>{translate('NoIssuesWithYourConfiguration')}</span>
          </Cluster>
        </Surface>
      ) : null}

      {healthIssues ? (
        <>
          <Table columns={columns}>
            <TableBody>
              {items.map((item) => {
                const source = item.source;
                const presentation = getHealthPresentation(item.type);

                return (
                  <TableRow key={`health${item.message}`}>
                    <TableRowCell>
                      <StatusBadge
                        icon={presentation.icon}
                        label={presentation.label}
                        tone={presentation.tone}
                      />
                    </TableRowCell>

                    <TableRowCell>{item.message}</TableRowCell>

                    <TableRowCell className={styles.actions}>
                      <Cluster className={styles.actionCluster} gap="small">
                        <IconButton
                          aria-label={translate(
                            'ReadTheWikiForMoreInformation'
                          )}
                          name={icons.WIKI}
                          to={item.wikiUrl}
                          title={translate('ReadTheWikiForMoreInformation')}
                        />

                        <HealthItemLink source={source} />

                        {source === 'IndexerStatusCheck' ||
                        source === 'IndexerLongTermStatusCheck' ? (
                          <SpinnerIconButton
                            aria-label={translate('TestAll')}
                            name={icons.TEST}
                            title={translate('TestAll')}
                            isSpinning={isTestingAllIndexers}
                            onPress={handleTestAllIndexersPress}
                          />
                        ) : null}

                        {source === 'DownloadClientCheck' ||
                        source === 'DownloadClientStatusCheck' ? (
                          <SpinnerIconButton
                            aria-label={translate('TestAll')}
                            name={icons.TEST}
                            title={translate('TestAll')}
                            isSpinning={isTestingAllDownloadClients}
                            onPress={handleTestAllDownloadClientsPress}
                          />
                        ) : null}
                      </Cluster>
                    </TableRowCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>

          <Alert kind={kinds.INFO}>
            <InlineMarkdown
              data={translate('HealthMessagesInfoBox', {
                link: '/system/logs/files',
              })}
            />
          </Alert>
        </>
      ) : null}
    </FieldSet>
  );
}

export default Health;
