import React, { ReactNode } from 'react';
import { StatusBadge, type StatusTone } from 'Components/Status';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import type { HygieneAssessment } from './useHygienePreview';

type HygieneAttentionTone = Extract<
  StatusTone,
  'danger' | 'warning' | 'monitor' | 'info'
>;

const reasonTones: Record<string, HygieneAttentionTone> = {
  'client-failed': 'danger',
  'import-blocked': 'danger',
  'client-unavailable': 'warning',
  'download-warning': 'warning',
  'download-stalled': 'warning',
  'download-metadata': 'warning',
  'download-slow': 'warning',
  'download-unstable': 'warning',
  pending: 'info',
};

const toneRanks: Record<HygieneAttentionTone, number> = {
  danger: 0,
  warning: 1,
  monitor: 2,
  info: 3,
};

const toneIcons = {
  danger: icons.DANGER,
  warning: icons.WARNING,
  monitor: icons.VIEW,
  info: icons.INFO,
};

export interface HygieneAttention {
  count: number;
  label: string;
  tone: HygieneAttentionTone;
}

export function getHygieneAttention(
  assessments: readonly HygieneAssessment[]
): HygieneAttention {
  const attentionItems = assessments.filter((item) => item.needsAttention);
  const count = attentionItems.length;
  const tone = attentionItems.reduce<HygieneAttentionTone>(
    (currentTone, item) => {
      const itemTone = reasonTones[item.reasonCode] ?? 'info';

      return toneRanks[itemTone] < toneRanks[currentTone]
        ? itemTone
        : currentTone;
    },
    'info'
  );

  return {
    count,
    label: translate(
      count === 1
        ? 'HygieneDownloadNeedsAttention'
        : 'HygieneDownloadsNeedAttention',
      { count }
    ),
    tone,
  };
}

interface HygieneAttentionBadgeProps {
  assessments?: readonly HygieneAssessment[];
  compact?: boolean;
}

export default function HygieneAttentionBadge({
  assessments,
  compact = false,
}: HygieneAttentionBadgeProps) {
  if (!assessments) {
    return null;
  }

  const { count, label, tone } = getHygieneAttention(assessments);

  if (count === 0) {
    return null;
  }

  let badgeLabel: ReactNode = label;

  if (compact) {
    badgeLabel = <span aria-hidden={true}>{count}</span>;
  }

  return (
    <StatusBadge
      aria-label={label}
      icon={toneIcons[tone]}
      label={badgeLabel}
      tone={tone}
    />
  );
}
