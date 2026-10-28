import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import Alert from 'Components/Alert';
import { Stack } from 'Components/Layout';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Column from 'Components/Table/Column';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { Heading, Text } from 'Components/Typography';
import { kinds } from 'Helpers/Props';
import { fetchCommands } from 'Store/Actions/commandActions';
import translate from 'Utilities/String/translate';
import QueuedTaskRow from './QueuedTaskRow';

const columns: Column[] = [
  {
    name: 'trigger',
    label: '',
    isVisible: true,
  },
  {
    name: 'commandName',
    label: () => translate('Name'),
    isVisible: true,
  },
  {
    name: 'queued',
    label: () => translate('Queued'),
    isVisible: true,
  },
  {
    name: 'started',
    label: () => translate('Started'),
    isVisible: true,
  },
  {
    name: 'ended',
    label: () => translate('Ended'),
    isVisible: true,
  },
  {
    name: 'duration',
    label: () => translate('Duration'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isVisible: true,
  },
];

export default function QueuedTasks() {
  const dispatch = useDispatch();
  const { isFetching, isPopulated, error, items } = useSelector(
    (state: AppState) => state.commands
  );

  useEffect(() => {
    dispatch(fetchCommands());
  }, [dispatch]);

  return (
    <Stack
      component="section"
      gap="medium"
      aria-labelledby="task-queue-heading"
    >
      <Heading id="task-queue-heading" level={2}>
        {translate('Queue')}
      </Heading>

      {isFetching && !isPopulated && <LoadingIndicator />}

      {!isFetching && error ? (
        <Alert role="alert" kind={kinds.DANGER}>
          {translate('TaskQueueLoadError')}
        </Alert>
      ) : null}

      {isPopulated && !error && items.length === 0 ? (
        <Text tone="secondary">{translate('TaskQueueEmpty')}</Text>
      ) : null}

      {isPopulated && !error && items.length > 0 ? (
        <Table columns={columns}>
          <TableBody>
            {items.map((item) => {
              return <QueuedTaskRow key={item.id} {...item} />;
            })}
          </TableBody>
        </Table>
      ) : null}
    </Stack>
  );
}
