import React, { useCallback, useMemo } from 'react';
import { InputChanged } from 'typings/inputs';
import shortenList from 'Utilities/String/shortenList';
import titleCase from 'Utilities/String/titleCase';
import SelectInput from '../SelectInput';

interface AlbumRelease {
  foreignReleaseId: string;
  title: string;
  disambiguation?: string;
  mediumCount: number;
  trackCount: number;
  country?: string[];
  format?: string;
  monitored: boolean;
}

export interface AlbumReleaseSelectInputProps {
  name: string;
  albumReleases: { value: AlbumRelease[] };
  isDisabled?: boolean;
  hasError?: boolean;
  hasWarning?: boolean;
  onChange: (change: InputChanged<AlbumRelease[]>) => void;
}

function getLabel(albumRelease: AlbumRelease) {
  const { title, disambiguation, mediumCount, trackCount, country, format } =
    albumRelease;

  return (
    `${title}` +
    `${disambiguation ? ' (' : ''}${titleCase(disambiguation)}${
      disambiguation ? ')' : ''
    }` +
    `, ${mediumCount} med, ${trackCount} tracks` +
    `${country && country.length > 0 ? `, ${shortenList(country)}` : ''}` +
    `${format ? `, [${format}]` : ''}`
  );
}

function AlbumReleaseSelectInput({
  name,
  albumReleases,
  onChange,
  ...otherProps
}: AlbumReleaseSelectInputProps) {
  const releases = albumReleases.value;

  const values = useMemo(() => {
    return releases
      .map((albumRelease) => ({
        key: albumRelease.foreignReleaseId,
        value: getLabel(albumRelease),
      }))
      .sort((a, b) => a.value.localeCompare(b.value));
  }, [releases]);

  const value = releases.find((r) => r.monitored)?.foreignReleaseId ?? '';

  const handleChange = useCallback(
    ({ value: foreignReleaseId }: InputChanged<string>) => {
      const updatedReleases = releases.map((release) => ({
        ...release,
        monitored: release.foreignReleaseId === foreignReleaseId,
      }));

      onChange({ name, value: updatedReleases });
    },
    [name, releases, onChange]
  );

  return (
    <SelectInput
      {...otherProps}
      name={name}
      value={value}
      values={values}
      onChange={handleChange}
    />
  );
}

export default AlbumReleaseSelectInput;
