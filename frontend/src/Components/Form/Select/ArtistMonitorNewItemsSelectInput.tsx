import React from 'react';
import monitorNewItemsOptions from 'Utilities/Artist/monitorNewItemsOptions';
import EnhancedSelectInput, {
  EnhancedSelectInputProps,
  EnhancedSelectInputValue,
} from './EnhancedSelectInput';

export interface ArtistMonitorNewItemsSelectInputProps
  extends Omit<
    EnhancedSelectInputProps<EnhancedSelectInputValue<string>, string>,
    'values'
  > {
  includeNoChange?: boolean;
  includeNoChangeDisabled?: boolean;
  includeMixed?: boolean;
}

function ArtistMonitorNewItemsSelectInput(
  props: ArtistMonitorNewItemsSelectInputProps
) {
  const {
    includeNoChange = false,
    includeNoChangeDisabled = true,
    includeMixed = false,
    ...otherProps
  } = props;

  const values: EnhancedSelectInputValue<string>[] = [
    ...monitorNewItemsOptions,
  ];

  if (includeNoChange) {
    values.unshift({
      key: 'noChange',
      value: 'No Change',
      isDisabled: includeNoChangeDisabled,
    });
  }

  if (includeMixed) {
    values.unshift({
      key: 'mixed',
      value: '(Mixed)',
      isDisabled: true,
    });
  }

  return <EnhancedSelectInput {...otherProps} values={values} />;
}

export default ArtistMonitorNewItemsSelectInput;
