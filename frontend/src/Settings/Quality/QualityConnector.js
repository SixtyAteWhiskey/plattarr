import React from 'react';
import { useSelector } from 'react-redux';
import * as commandNames from 'Commands/commandNames';
import { useMediaKind } from 'Helpers/mediaKindStore';
import createCommandExecutingSelector from 'Store/Selectors/createCommandExecutingSelector';
import Quality from './Quality';

const isResettingVideoSelector = createCommandExecutingSelector(commandNames.RESET_QUALITY_DEFINITIONS);
const isResettingAudioSelector = createCommandExecutingSelector(commandNames.RESET_AUDIO_QUALITY_DEFINITIONS);

// Movies and Series share one set of definitions; Music has its own. The page
// is keyed by that choice so its pending-change state starts clean per set.
function QualityConnector() {
  const isAudio = useMediaKind() === 'music';
  const isResettingVideo = useSelector(isResettingVideoSelector);
  const isResettingAudio = useSelector(isResettingAudioSelector);

  return (
    <Quality
      key={isAudio ? 'audio' : 'video'}
      isAudio={isAudio}
      isResettingQualityDefinitions={isAudio ? isResettingAudio : isResettingVideo}
    />
  );
}

export default QualityConnector;
