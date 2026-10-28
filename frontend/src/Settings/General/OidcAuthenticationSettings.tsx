import React from 'react';
import FormGroup from 'Components/Form/FormGroup';
import FormInputGroup from 'Components/Form/FormInputGroup';
import FormLabel from 'Components/Form/FormLabel';
import { inputTypes } from 'Helpers/Props';
import { InputChanged } from 'typings/inputs';
import { PendingSection } from 'typings/pending';
import General from 'typings/Settings/General';
import translate from 'Utilities/String/translate';

interface OidcAuthenticationSettingsProps {
  oidcAuthority: PendingSection<General>['oidcAuthority'];
  oidcClientId: PendingSection<General>['oidcClientId'];
  oidcClientSecret: PendingSection<General>['oidcClientSecret'];
  oidcUserIdentifier: PendingSection<General>['oidcUserIdentifier'];
  oidcScopes: PendingSection<General>['oidcScopes'];
  showValidationWarnings?: boolean;
  onInputChange: (change: InputChanged) => void;
}

function OidcAuthenticationSettings({
  oidcAuthority,
  oidcClientId,
  oidcClientSecret,
  oidcUserIdentifier,
  oidcScopes,
  showValidationWarnings = false,
  onInputChange,
}: OidcAuthenticationSettingsProps) {
  return (
    <>
      <FormGroup>
        <FormLabel name="oidcAuthority">{translate('OidcAuthority')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="oidcAuthority"
          helpText={translate('OidcAuthorityHelpText')}
          helpTextWarning={
            showValidationWarnings && !oidcAuthority?.value
              ? translate('AuthenticationRequiredOidcAuthorityHelpTextWarning')
              : undefined
          }
          onChange={onInputChange}
          {...oidcAuthority}
          id="oidcAuthority"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="oidcClientId">{translate('OidcClientId')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="oidcClientId"
          helpText={translate('OidcClientIdHelpText')}
          helpTextWarning={
            showValidationWarnings && !oidcClientId?.value
              ? translate('AuthenticationRequiredOidcClientIdHelpTextWarning')
              : undefined
          }
          onChange={onInputChange}
          {...oidcClientId}
          id="oidcClientId"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="oidcClientSecret">
          {translate('OidcClientSecret')}
        </FormLabel>

        <FormInputGroup
          type={inputTypes.PASSWORD}
          name="oidcClientSecret"
          helpText={translate('OidcClientSecretHelpText')}
          helpTextWarning={
            showValidationWarnings && !oidcClientSecret?.value
              ? translate(
                  'AuthenticationRequiredOidcClientSecretHelpTextWarning'
                )
              : undefined
          }
          onChange={onInputChange}
          {...oidcClientSecret}
          id="oidcClientSecret"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="oidcUserIdentifier">
          {translate('OidcUserIdentifier')}
        </FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="oidcUserIdentifier"
          helpText={translate('OidcUserIdentifierHelpText')}
          helpTextWarning={
            showValidationWarnings && !oidcUserIdentifier?.value
              ? translate(
                  'AuthenticationRequiredOidcUserIdentifierHelpTextWarning'
                )
              : undefined
          }
          onChange={onInputChange}
          {...oidcUserIdentifier}
          id="oidcUserIdentifier"
        />
      </FormGroup>

      <FormGroup>
        <FormLabel name="oidcScopes">{translate('OidcScopes')}</FormLabel>

        <FormInputGroup
          type={inputTypes.TEXT}
          name="oidcScopes"
          helpText={translate('OidcScopesHelpText')}
          onChange={onInputChange}
          {...oidcScopes}
          id="oidcScopes"
        />
      </FormGroup>
    </>
  );
}

export default OidcAuthenticationSettings;
