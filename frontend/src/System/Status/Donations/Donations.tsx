import React from 'react';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';
import styles from '../styles.css';

// Plattarr is built on these projects; the section links to their donation
// pages as text (no upstream logos).
const upstreamProjects = [
  { name: 'Radarr', url: 'https://radarr.video/donate' },
  { name: 'Sonarr', url: 'https://sonarr.tv/donate' },
  { name: 'Lidarr', url: 'https://lidarr.audio/donate' },
  { name: 'Prowlarr', url: 'https://prowlarr.com/donate' },
];

function Donations() {
  return (
    <FieldSet legend={translate('Donations')}>
      <div className={styles.upstream}>
        {upstreamProjects.map(({ name, url }) => (
          <Link key={name} to={url}>
            {name}
          </Link>
        ))}
      </div>
    </FieldSet>
  );
}

export default Donations;
