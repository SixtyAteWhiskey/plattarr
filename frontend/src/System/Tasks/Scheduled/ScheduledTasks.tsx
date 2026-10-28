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
import { fetchTasks } from 'Store/Actions/systemActions';
import translate from 'Utilities/String/translate';
import ScheduledTaskRow from './ScheduledTaskRow';

const columns: Column[] = [
  {
    name: 'name',
    label: () => translate('Name'),
    isVisible: true,
  },
  {
    name: 'interval',
    label: () => translate('Interval'),
    isVisible: true,
  },
  {
    name: 'lastExecution',
    label: () => translate('LastExecution'),
    isVisible: true,
  },
  {
    name: 'lastDuration',
    label: () => translate('LastDuration'),
    isVisible: true,
  },
  {
    name: 'nextExecution',
    label: () => translate('NextExecution'),
    isVisible: true,
  },
  {
    name: 'actions',
    label: '',
    isVisible: true,
  },
];

function ScheduledTasks() {
  const dispatch = useDispatch();
  const { isFetching, isPopulated, error, items } = useSelector(
    (state: AppState) => state.system.tasks
  );

  useEffect(() => {
    dispatch(fetchTasks());
  }, [dispatch]);

  return (
    <Stack
      component="section"
      gap="medium"
      aria-labelledby="scheduled-tasks-heading"
    >
      <Heading id="scheduled-tasks-heading" level={2}>
        {translate('Scheduled')}
      </Heading>

      {isFetching && !isPopulated && <LoadingIndicator />}

      {!isFetching && error ? (
        <Alert role="alert" kind={kinds.DANGER}>
          {translate('ScheduledTasksLoadError')}
        </Alert>
      ) : null}

      {isPopulated && !error && items.length === 0 ? (
        <Text tone="secondary">{translate('ScheduledTasksEmpty')}</Text>
      ) : null}

      {isPopulated && !error && items.length > 0 ? (
        <Table columns={columns}>
          <TableBody>
            {items.map((item) => {
              return <ScheduledTaskRow key={item.id} {...item} />;
            })}
          </TableBody>
        </Table>
      ) : null}
    </Stack>
  );
}

export default ScheduledTasks;
