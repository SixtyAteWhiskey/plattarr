import React from 'react';
import PageIntro from 'Components/Page/PageIntro';
import translate from 'Utilities/String/translate';

interface SettingsPageHeaderProps {
  title: string;
  summary: string;
}

function SettingsPageHeader({ title, summary }: SettingsPageHeaderProps) {
  return (
    <PageIntro title={translate(title)} description={translate(summary)} />
  );
}

export default SettingsPageHeader;
