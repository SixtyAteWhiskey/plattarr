import React from 'react';
import IconButton from 'Components/Link/IconButton';
import { icons } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

interface HealthItemLinkProps {
  source: string;
}

function HealthItemLink(props: HealthItemLinkProps) {
  const { source } = props;

  switch (source) {
    case 'IndexerRssCheck':
    case 'IndexerSearchCheck':
    case 'IndexerStatusCheck':
    case 'IndexerJackettAllCheck':
    case 'IndexerLongTermStatusCheck':
      return (
        <IconButton
          aria-label={translate('Settings')}
          name={icons.SETTINGS}
          title={translate('Settings')}
          to="/settings/indexers"
        />
      );
    case 'DownloadClientCheck':
    case 'DownloadClientStatusCheck':
    case 'ImportMechanismCheck':
      return (
        <IconButton
          aria-label={translate('Settings')}
          name={icons.SETTINGS}
          title={translate('Settings')}
          to="/settings/downloadclients"
        />
      );
    case 'NotificationStatusCheck':
      return (
        <IconButton
          aria-label={translate('Settings')}
          name={icons.SETTINGS}
          title={translate('Settings')}
          to="/settings/connect"
        />
      );
    case 'MovieCollectionRootFolderCheck':
      return (
        <IconButton
          aria-label={translate('Collections')}
          name={icons.MOVIE_CONTINUING}
          title={translate('Collections')}
          to="/collections"
        />
      );
    case 'RootFolderCheck':
      return (
        <IconButton
          aria-label={translate('MovieEditor')}
          name={icons.MOVIE_CONTINUING}
          title={translate('MovieEditor')}
          to="/"
        />
      );
    case 'UpdateCheck':
      return (
        <IconButton
          aria-label={translate('Updates')}
          name={icons.UPDATE}
          title={translate('Updates')}
          to="/system/updates"
        />
      );
    default:
      return null;
  }
}

export default HealthItemLink;
