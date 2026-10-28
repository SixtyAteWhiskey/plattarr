import React from 'react';
import FieldSet from 'Components/FieldSet';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { EnhancedSelectInputValue } from 'Components/Form/Select/EnhancedSelectInput';
import { inputTypes, sizes } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import { PendingSection } from 'typings/pending';
import General from 'typings/Settings/General';
import translate from 'Utilities/String/translate';

interface ProxySettingsProps {
  proxyEnabled: PendingSection<General>['proxyEnabled'];
  proxyType: PendingSection<General>['proxyType'];
  proxyHostname: PendingSection<General>['proxyHostname'];
  proxyPort: PendingSection<General>['proxyPort'];
  proxyUsername: PendingSection<General>['proxyUsername'];
  proxyPassword: PendingSection<General>['proxyPassword'];
  proxyBypassFilter: PendingSection<General>['proxyBypassFilter'];
  proxyBypassLocalAddresses: PendingSection<General>['proxyBypassLocalAddresses'];
  onInputChange: (change: InputChanged) => void;
}

function ProxySettings({
  proxyEnabled,
  proxyType,
  proxyHostname,
  proxyPort,
  proxyUsername,
  proxyPassword,
  proxyBypassFilter,
  proxyBypassLocalAddresses,
  onInputChange,
}: ProxySettingsProps) {
  const proxyTypeOptions: EnhancedSelectInputValue<string>[] = [
    {
      key: 'http',
      value: translate('HttpHttps'),
    },
    {
      key: 'socks4',
      value: translate('Socks4'),
    },
    {
      key: 'socks5',
      value: translate('Socks5'),
    },
  ];

  return (
    <FieldSet legend={translate('Proxy')}>
      <FormGroup size={sizes.MEDIUM}>
        <FormLabel name="proxyEnabled">{translate('UseProxy')}</FormLabel>

        <FormInputGroup
          type={inputTypes.CHECK}
          name="proxyEnabled"
          onChange={onInputChange}
          {...proxyEnabled}
          id="proxyEnabled"
        />
      </FormGroup>

      {proxyEnabled.value && (
        <div>
          <FormGroup>
            <FormLabel name="proxyType">{translate('ProxyType')}</FormLabel>

            <FormInputGroup
              type={inputTypes.SELECT}
              name="proxyType"
              values={proxyTypeOptions}
              onChange={onInputChange}
              {...proxyType}
              id="proxyType"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="proxyHostname">{translate('Hostname')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="proxyHostname"
              onChange={onInputChange}
              {...proxyHostname}
              id="proxyHostname"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="proxyPort">{translate('Port')}</FormLabel>

            <FormInputGroup
              type={inputTypes.NUMBER}
              name="proxyPort"
              min={1}
              max={65535}
              onChange={onInputChange}
              {...proxyPort}
              id="proxyPort"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="proxyUsername">{translate('Username')}</FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="proxyUsername"
              helpText={translate('ProxyUsernameHelpText')}
              onChange={onInputChange}
              {...proxyUsername}
              id="proxyUsername"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="proxyPassword">{translate('Password')}</FormLabel>

            <FormInputGroup
              type={inputTypes.PASSWORD}
              name="proxyPassword"
              helpText={translate('ProxyPasswordHelpText')}
              onChange={onInputChange}
              {...proxyPassword}
              id="proxyPassword"
            />
          </FormGroup>

          <FormGroup>
            <FormLabel name="proxyBypassFilter">
              {translate('IgnoredAddresses')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.TEXT}
              name="proxyBypassFilter"
              helpText={translate('ProxyBypassFilterHelpText')}
              onChange={onInputChange}
              {...proxyBypassFilter}
              id="proxyBypassFilter"
            />
          </FormGroup>

          <FormGroup size={sizes.MEDIUM}>
            <FormLabel name="proxyBypassLocalAddresses">
              {translate('BypassProxyForLocalAddresses')}
            </FormLabel>

            <FormInputGroup
              type={inputTypes.CHECK}
              name="proxyBypassLocalAddresses"
              onChange={onInputChange}
              {...proxyBypassLocalAddresses}
              id="proxyBypassLocalAddresses"
            />
          </FormGroup>
        </div>
      )}
    </FieldSet>
  );
}

export default ProxySettings;
