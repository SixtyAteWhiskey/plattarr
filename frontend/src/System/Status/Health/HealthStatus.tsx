import React, { useEffect } from 'react';
import { useDispatch, useSelector } from 'react-redux';
import AppState from 'App/State/AppState';
import PageSidebarStatus from 'Components/Page/Sidebar/PageSidebarStatus';
import usePrevious from 'Helpers/Hooks/usePrevious';
import { fetchHealth } from 'Store/Actions/systemActions';
import useHealthStatus from './useHealthStatus';

function HealthStatus() {
  const dispatch = useDispatch();
  const { isConnected, isReconnecting } = useSelector(
    (state: AppState) => state.app
  );
  const { count, errors, isPopulated, warnings } = useHealthStatus();

  const wasReconnecting = usePrevious(isReconnecting);

  useEffect(() => {
    if (!isPopulated) {
      dispatch(fetchHealth());
    }
  }, [isPopulated, dispatch]);

  useEffect(() => {
    if (isConnected && wasReconnecting) {
      dispatch(fetchHealth());
    }
  }, [isConnected, wasReconnecting, dispatch]);

  return (
    <PageSidebarStatus count={count} errors={errors} warnings={warnings} />
  );
}

export default HealthStatus;
