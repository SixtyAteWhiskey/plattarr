import React, { useCallback, useEffect } from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { QualityProfilesAppState } from 'App/State/SettingsAppState';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import { EnhancedSelectInputChanged } from 'typings/inputs';
import MediaKind from 'typings/MediaKind';
import QualityProfile from 'typings/QualityProfile';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';

function createQualityProfilesSelector(
  includeNoChange: boolean,
  includeNoChangeDisabled: boolean,
  includeMixed: boolean,
  mediaKind?: MediaKind
) {
  return createSelector(
    createSortedSectionSelector<QualityProfile, QualityProfilesAppState>(
      'settings.qualityProfiles',
      sortByProp<QualityProfile, 'name'>('name')
    ),
    (qualityProfiles: QualityProfilesAppState) => {
      const values: EnhancedSelectInputValue<number | string>[] =
        qualityProfiles.items
          .filter(
            (qualityProfile) =>
              !mediaKind ||
              !qualityProfile.appliesTo?.length ||
              qualityProfile.appliesTo.includes(mediaKind)
          )
          .map((qualityProfile) => ({
            key: qualityProfile.id,
            value: qualityProfile.name,
          }));

      if (includeNoChange) {
        values.unshift({
          key: 'noChange',
          get value() {
            return translate('NoChange');
          },
          isDisabled: includeNoChangeDisabled,
        });
      }

      if (includeMixed) {
        values.unshift({
          key: 'mixed',
          get value() {
            return `(${translate('Mixed')})`;
          },
          isDisabled: true,
        });
      }

      return values;
    }
  );
}

export interface QualityProfileSelectInputProps
  extends Omit<
    EnhancedSelectInputProps<
      EnhancedSelectInputValue<number | string>,
      number | string
    >,
    'values'
  > {
  name: string;
  includeNoChange?: boolean;
  includeNoChangeDisabled?: boolean;
  includeMixed?: boolean;
  mediaKind?: MediaKind;
}

function QualityProfileSelectInput({
  name,
  value,
  includeNoChange = false,
  includeNoChangeDisabled = true,
  includeMixed = false,
  mediaKind,
  onChange,
  ...otherProps
}: QualityProfileSelectInputProps) {
  const values = useSelector(
    createQualityProfilesSelector(
      includeNoChange,
      includeNoChangeDisabled,
      includeMixed,
      mediaKind
    )
  );

  const handleChange = useCallback(
    ({ value }: EnhancedSelectInputChanged<string | number>) => {
      onChange({ name, value });
    },
    [name, onChange]
  );

  useEffect(() => {
    if (!value || !values.some((option) => option.key === value)) {
      const firstValue = values.find(
        (option) => typeof option.key === 'number'
      );

      if (firstValue) {
        onChange({ name, value: firstValue.key });
      }
    }
  }, [name, value, values, onChange]);

  return (
    <EnhancedSelectInput
      {...otherProps}
      name={name}
      value={value}
      values={values}
      onChange={handleChange}
    />
  );
}

export default QualityProfileSelectInput;
