import React from 'react';
import { useSelector } from 'react-redux';
import { createAudioQualityProfileSelectorForHook } from 'Store/Selectors/createAudioQualityProfileSelector';
import translate from 'Utilities/String/translate';

interface AudioQualityProfileNameProps {
  qualityProfileId: number;
}

function AudioQualityProfileName({
  qualityProfileId,
}: AudioQualityProfileNameProps) {
  const qualityProfile = useSelector(
    createAudioQualityProfileSelectorForHook(qualityProfileId)
  );

  return <span>{qualityProfile?.name ?? translate('Unknown')}</span>;
}

export default AudioQualityProfileName;
