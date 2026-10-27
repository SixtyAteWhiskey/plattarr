import useApiQuery from 'Helpers/Hooks/useApiQuery';

export interface HygieneAssessment {
  queueId: number;
  ruleId: number | null;
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
  actionExplanation: string;
}

const REFRESH_INTERVAL = 15000;

interface UseHygienePreviewOptions {
  poll: boolean;
}

export default function useHygienePreview({ poll }: UseHygienePreviewOptions) {
  return useApiQuery<HygieneAssessment[]>({
    path: '/hygiene/preview',
    queryOptions: {
      refetchInterval: poll ? REFRESH_INTERVAL : false,
      // Keep the shared result fresh until the sidebar-owned polling cycle.
      staleTime: REFRESH_INTERVAL,
    },
  });
}
