import React, { useCallback } from 'react';
import Button from 'Components/Link/Button';
import { setMediaKind, useMediaKind } from 'Helpers/mediaKindStore';
import { kinds } from 'Helpers/Props';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import styles from './MediaKindSwitcher.css';

const OPTIONS: { kind: MediaKind; label: () => string }[] = [
  { kind: 'movies', label: () => translate('Movies') },
  { kind: 'tv', label: () => translate('Series') },
  { kind: 'music', label: () => translate('Music') },
];

function MediaKindSwitcher() {
  const mediaKind = useMediaKind();

  const handlePress = useCallback((kind: MediaKind) => {
    setMediaKind(kind);
  }, []);

  return (
    <div className={styles.switcher}>
      {OPTIONS.map(({ kind, label }) => {
        return (
          <MediaKindSwitcherButton
            key={kind}
            kind={kind}
            label={label()}
            isActive={mediaKind === kind}
            onPress={handlePress}
          />
        );
      })}
    </div>
  );
}

interface MediaKindSwitcherButtonProps {
  kind: MediaKind;
  label: string;
  isActive: boolean;
  onPress: (kind: MediaKind) => void;
}

function MediaKindSwitcherButton({
  kind,
  label,
  isActive,
  onPress,
}: MediaKindSwitcherButtonProps) {
  const handlePress = useCallback(() => {
    onPress(kind);
  }, [kind, onPress]);

  return (
    <Button
      kind={isActive ? kinds.PRIMARY : kinds.DEFAULT}
      onPress={handlePress}
    >
      {label}
    </Button>
  );
}

export default MediaKindSwitcher;
