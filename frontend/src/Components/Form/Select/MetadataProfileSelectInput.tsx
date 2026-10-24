import React, { useCallback, useEffect } from 'react';
import { useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { MetadataProfilesAppState } from 'App/State/SettingsAppState';
import { metadataProfileNames } from 'Helpers/Props';
import createSortedSectionSelector from 'Store/Selectors/createSortedSectionSelector';
import { EnhancedSelectInputChanged } from 'typings/inputs';
import MetadataProfile from 'typings/MetadataProfile';
import sortByProp from 'Utilities/Array/sortByProp';
import translate from 'Utilities/String/translate';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';

function createMetadataProfilesSelector(
  includeNoChange: boolean,
  includeNoChangeDisabled: boolean,
  includeMixed: boolean,
  includeNone: boolean
) {
  return createSelector(
    createSortedSectionSelector<MetadataProfile, MetadataProfilesAppState>(
      'settings.metadataProfiles',
      sortByProp<MetadataProfile, 'name'>('name')
    ),
    (metadataProfiles: MetadataProfilesAppState) => {
      const profiles = metadataProfiles.items.filter(
        (item) => item.name !== metadataProfileNames.NONE
      );

      const noneProfile = metadataProfiles.items.find(
        (item) => item.name === metadataProfileNames.NONE
      );

      const values: EnhancedSelectInputValue<number | string>[] = profiles.map(
        (metadataProfile) => {
          return {
            key: metadataProfile.id,
            value: metadataProfile.name,
          };
        }
      );

      if (includeNone && noneProfile) {
        values.push({
          key: noneProfile.id,
          value: noneProfile.name,
        });
      }

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

export interface MetadataProfileSelectInputProps
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
  includeNone?: boolean;
}

function MetadataProfileSelectInput({
  name,
  value,
  includeNoChange = false,
  includeNoChangeDisabled = true,
  includeMixed = false,
  includeNone = true,
  onChange,
  ...otherProps
}: MetadataProfileSelectInputProps) {
  const values = useSelector(
    createMetadataProfilesSelector(
      includeNoChange,
      includeNoChangeDisabled,
      includeMixed,
      includeNone
    )
  );

  const handleChange = useCallback(
    ({ value: newValue }: EnhancedSelectInputChanged<string | number>) => {
      onChange({
        name,
        value: newValue === 'noChange' ? newValue : Number(newValue),
      });
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

export default MetadataProfileSelectInput;
