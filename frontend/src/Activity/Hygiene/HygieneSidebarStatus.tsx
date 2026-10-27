import React from 'react';
import HygieneAttentionBadge from './HygieneAttentionBadge';
import useHygienePreview from './useHygienePreview';

export default function HygieneSidebarStatus() {
  const { data } = useHygienePreview({ poll: true });

  return <HygieneAttentionBadge assessments={data} compact={true} />;
}
