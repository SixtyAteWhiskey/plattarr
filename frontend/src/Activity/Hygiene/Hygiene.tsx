import React, { useCallback } from 'react';
import { Link } from 'react-router-dom';
import Alert from 'Components/Alert';
import FieldSet from 'Components/FieldSet';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import useApiQuery from 'Helpers/Hooks/useApiQuery';
import HygieneRules from './HygieneRules';

interface HygieneAssessment {
  queueId: number;
  title: string;
  kind?: string;
  downloadClient: string;
  reasonCode: string;
  explanation: string;
  recommendedAction: string;
  needsAttention: boolean;
  strikes: number;
  strikeLimit?: number;
  ruleEnabled: boolean;
  eligible: boolean;
  ruleExplanation: string;
  wouldAct: boolean;
}

function Hygiene() {
  const { data, isLoading, isError, refetch } = useApiQuery<
    HygieneAssessment[]
  >({
    path: '/hygiene/preview',
    queryOptions: { refetchInterval: 15000 },
  });

  const handleRefresh = useCallback(() => {
    refetch();
  }, [refetch]);

  return (
    <PageContent title="Hygiene">
      <PageContentBody>
        <h1>Hygiene</h1>
        <Alert>
          Queue preview is read only. No automatic cleanup is enabled by this
          page. Review an item in the <Link to="/activity/queue">queue</Link>{' '}
          before taking action.
        </Alert>
        <p>
          <button type="button" onClick={handleRefresh}>
            Refresh preview
          </button>
        </p>
        {isLoading && <LoadingIndicator />}
        {isError && (
          <Alert kind="danger">Unable to load the queue preview.</Alert>
        )}
        {data && data.length === 0 && <p>The queue is empty.</p>}
        {data && data.length > 0 && (
          <p>
            {data.filter((item) => item.needsAttention).length} of {data.length}{' '}
            downloads need attention.
          </p>
        )}
        {data?.map((item) => (
          <FieldSet
            key={item.queueId}
            legend={item.title || 'Untitled download'}
          >
            <p>
              {item.kind || 'Unmatched'} ·{' '}
              {item.downloadClient || 'Unknown client'}
            </p>
            <p>
              <strong>
                {item.needsAttention ? 'Needs attention' : 'No issue detected'}{' '}
                · {item.reasonCode}
              </strong>
              : {item.explanation}
            </p>
            <p>{item.recommendedAction}</p>
            <p>{item.ruleExplanation}</p>
            {item.ruleEnabled && (
              <p>
                Strikes: {item.strikes} / {item.strikeLimit}
                {item.eligible && ' · Threshold reached'}
              </p>
            )}
            <p>{item.wouldAct ? 'Action proposed' : 'No automatic action'}</p>
          </FieldSet>
        ))}
        <HygieneRules onChanged={handleRefresh} />
      </PageContentBody>
    </PageContent>
  );
}

export default Hygiene;
