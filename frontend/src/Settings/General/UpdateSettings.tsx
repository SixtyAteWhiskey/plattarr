import React from 'react';
import FieldSet from 'Components/FieldSet';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import useShowAdvancedSettings from 'Helpers/Hooks/useShowAdvancedSettings';
import { inputTypes, sizes } from 'Helpers/Props';
import useSystemStatus from 'System/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import { PendingSection } from 'typings/pending';
import General from 'typings/Settings/General';
import titleCase from 'Utilities/String/titleCase';
import translate from 'Utilities/String/translate';

const branchValues = ['master', 'develop', 'nightly'];

interface UpdateSettingsProps {
  branch: PendingSection<General>['branch'];
  updateAutomatically: PendingSection<General>['updateAutomatically'];
  updateMechanism: PendingSection<General>['updateMechanism'];
  updateScriptPath: PendingSection<General>['updateScriptPath'];
  onInputChange: (change: InputChanged) => void;
}

function UpdateSettings({
  branch,
  updateAutomatically,
  updateMechanism,
  updateScriptPath,
  onInputChange,
}: UpdateSettingsProps) {
  const showAdvancedSettings = useShowAdvancedSettings();
  const { packageUpdateMechanism } = useSystemStatus();

  if (!showAdvancedSettings) {
    return null;
  }

  const usingExternalUpdateMechanism = packageUpdateMechanism !== 'builtIn';

  const updateOptions: EnhancedSelectInputValue<string>[] = [];

  if (usingExternalUpdateMechanism) {
    updateOptions.push({
      key: packageUpdateMechanism,
      value: titleCase(packageUpdateMechanism),
    });
  } else {
    updateOptions.push({ key: 'builtIn', value: translate('BuiltIn') });
  }

  updateOptions.push({ key: 'script', value: translate('Script') });

  return (
    <FieldSet legend={translate('Updates')}>
      <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
        <FormLabel name="branch">{translate('Branch')}</FormLabel>

        <FormInputGroup
          type={inputTypes.AUTO_COMPLETE}
          name="branch"
          helpText={
            usingExternalUpdateMechanism
              ? translate('BranchUpdateMechanism')
              : translate('BranchUpdate')
          }
          helpLink="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md"
          {...branch}
          values={branchValues}
          readOnly={usingExternalUpdateMechanism}
          id="branch"
          onChange={onInputChange}
        />
      </FormGroup>

      <div>
        <FormGroup
          advancedSettings={showAdvancedSettings}
          isAdvanced={true}
          size={sizes.MEDIUM}
        >
          <FormLabel name="updateAutomatically">
            {translate('Automatic')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.CHECK}
            name="updateAutomatically"
            helpText={translate('UpdateAutomaticallyHelpText')}
            helpTextWarning={
              updateMechanism.value === 'docker'
                ? translate('AutomaticUpdatesDisabledDocker')
                : undefined
            }
            onChange={onInputChange}
            {...updateAutomatically}
            id="updateAutomatically"
          />
        </FormGroup>

        <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
          <FormLabel name="updateMechanism">{translate('Mechanism')}</FormLabel>

          <FormInputGroup
            type={inputTypes.SELECT}
            name="updateMechanism"
            values={updateOptions}
            helpText={translate('UpdateMechanismHelpText')}
            helpLink="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md"
            onChange={onInputChange}
            {...updateMechanism}
            id="updateMechanism"
          />
        </FormGroup>

        {updateMechanism.value === 'script' ? (
          <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
            <FormLabel name="updateScriptPath">
              {translate('ScriptPath')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="updateScriptPath"
              helpText={translate('UpdateScriptPathHelpText')}
              onChange={onInputChange}
              {...updateScriptPath}
              id="updateScriptPath"
            />
          </FormGroup>
        ) : null}
      </div>
    </FieldSet>
  );
}

export default UpdateSettings;
