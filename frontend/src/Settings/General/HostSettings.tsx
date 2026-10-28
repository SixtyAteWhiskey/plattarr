import React from 'react';
import FieldSet from 'Components/FieldSet';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import useShowAdvancedSettings from 'Helpers/Hooks/useShowAdvancedSettings';
import { inputTypes, sizes } from 'Helpers/Props';
import useSystemStatus from 'System/useSystemStatus';
import { InputChanged } from 'typings/inputs';
import { PendingSection } from 'typings/pending';
import General from 'typings/Settings/General';
import translate from 'Utilities/String/translate';

interface HostSettingsProps {
  bindAddress: PendingSection<General>['bindAddress'];
  port: PendingSection<General>['port'];
  urlBase: PendingSection<General>['urlBase'];
  instanceName: PendingSection<General>['instanceName'];
  applicationUrl: PendingSection<General>['applicationUrl'];
  allowedHosts: PendingSection<General>['allowedHosts'];
  enableSsl: PendingSection<General>['enableSsl'];
  sslPort: PendingSection<General>['sslPort'];
  sslKeyPath: PendingSection<General>['sslKeyPath'];
  sslCertPath: PendingSection<General>['sslCertPath'];
  sslCertPassword: PendingSection<General>['sslCertPassword'];
  launchBrowser: PendingSection<General>['launchBrowser'];
  onInputChange: (change: InputChanged) => void;
}

function HostSettings({
  bindAddress,
  port,
  urlBase,
  instanceName,
  applicationUrl,
  allowedHosts,
  enableSsl,
  sslPort,
  sslCertPath,
  sslKeyPath,
  sslCertPassword,
  launchBrowser,
  onInputChange,
}: HostSettingsProps) {
  const showAdvancedSettings = useShowAdvancedSettings();
  const { isWindows, mode } = useSystemStatus();

  return (
    <FieldSet legend={translate('Host')}>
      <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
        <FormLabel name="bindAddress">{translate('BindAddress')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="bindAddress"
          helpText={translate('BindAddressHelpText')}
          helpTextWarning={translate('RestartRequiredHelpTextWarning')}
          onChange={onInputChange}
          {...bindAddress}
          id="bindAddress"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="port">{translate('PortNumber')}</FormLabel>

        <FormInputGroup
          type={inputTypes.NUMBER}
          name="port"
          min={1}
          max={65535}
          autocomplete="off"
          helpTextWarning={translate('RestartRequiredHelpTextWarning')}
          onChange={onInputChange}
          {...port}
          id="port"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="urlBase">{translate('UrlBase')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="urlBase"
          helpText={translate('UrlBaseHelpText')}
          helpTextWarning={translate('RestartRequiredHelpTextWarning')}
          onChange={onInputChange}
          {...urlBase}
          id="urlBase"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="allowedHosts">{translate('AllowedHosts')}</FormLabel>
        <FormInputGroup
          type={inputTypes.TEXT}
          name="allowedHosts"
          helpText={translate('AllowedHostsHelpText')}
          helpTextWarning={translate('RestartRequiredHelpTextWarning')}
          helpLink="https://github.com/SixtyAteWhiskey/plattarr/blob/develop/docs/first-run.md"
          onChange={onInputChange}
          {...allowedHosts}
          id="allowedHosts"
        />
      </FormGroup>

      <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
        <FormLabel name="instanceName">{translate('InstanceName')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="instanceName"
          helpText={translate('InstanceNameHelpText')}
          helpTextWarning={translate('RestartRequiredHelpTextWarning')}
          onChange={onInputChange}
          {...instanceName}
          id="instanceName"
        />
      </FormGroup>

      <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
        <FormLabel name="applicationUrl">
          {translate('ApplicationURL')}
        </FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="applicationUrl"
          helpText={translate('ApplicationUrlHelpText')}
          onChange={onInputChange}
          {...applicationUrl}
          id="applicationUrl"
        />
      </FormGroup>

      <FormGroup
        advancedSettings={showAdvancedSettings}
        isAdvanced={true}
        size={sizes.MEDIUM}
      >
        <FormLabel name="enableSsl">{translate('EnableSsl')}</FormLabel>

        <FormInputGroup
          type={inputTypes.CHECK}
          name="enableSsl"
          helpText={translate('EnableSslHelpText')}
          onChange={onInputChange}
          {...enableSsl}
          id="enableSsl"
        />
      </FormGroup>

      {enableSsl.value ? (
        <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
          <FormLabel name="sslPort">{translate('SslPort')}</FormLabel>

          <FormInputGroup
            type={inputTypes.NUMBER}
            name="sslPort"
            min={1}
            max={65535}
            helpTextWarning={translate('RestartRequiredHelpTextWarning')}
            onChange={onInputChange}
            {...sslPort}
            id="sslPort"
          />
        </FormGroup>
      ) : null}

      {enableSsl.value ? (
        <>
          <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
            <FormLabel name="sslCertPath">{translate('SslCertPath')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="sslCertPath"
              helpText={translate('SslCertPathHelpText')}
              helpTextWarning={translate('RestartRequiredHelpTextWarning')}
              onChange={onInputChange}
              {...sslCertPath}
              id="sslCertPath"
            />
          </FormGroup>

          <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
            <FormLabel name="sslKeyPath">{translate('SslKeyPath')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="sslKeyPath"
              helpText={translate('SslKeyPathHelpText')}
              helpTextWarning={translate('RestartRequiredHelpTextWarning')}
              onChange={onInputChange}
              {...sslKeyPath}
              id="sslKeyPath"
            />
          </FormGroup>

          <FormGroup advancedSettings={showAdvancedSettings} isAdvanced={true}>
            <FormLabel name="sslCertPassword">
              {translate('SslCertPassword')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.PASSWORD}
              name="sslCertPassword"
              helpText={translate('SslCertPasswordHelpText')}
              helpTextWarning={translate('RestartRequiredHelpTextWarning')}
              onChange={onInputChange}
              {...sslCertPassword}
              id="sslCertPassword"
            />
          </FormGroup>
        </>
      ) : null}

      {isWindows && mode !== 'service' ? (
        <FormGroup size={sizes.MEDIUM}>
          <FormLabel name="launchBrowser">
            {translate('OpenBrowserOnStart')}
          </FormLabel>

          <FormInputGroup
            type={inputTypes.CHECK}
            name="launchBrowser"
            helpText={translate('OpenBrowserOnStartHelpText')}
            onChange={onInputChange}
            {...launchBrowser}
            id="launchBrowser"
          />
        </FormGroup>
      ) : null}
    </FieldSet>
  );
}

export default HostSettings;
