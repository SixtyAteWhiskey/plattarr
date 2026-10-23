import React from 'react';
import Button from 'Components/Link/Button';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';
import styles from './NoSeries.css';

interface NoSeriesProps {
  totalItems: number;
}

function NoSeries(props: NoSeriesProps) {
  const { totalItems } = props;

  if (totalItems > 0) {
    return (
      <div>
        <div className={styles.message}>
          {translate('AllSeriesAreHiddenByTheAppliedFilter')}
        </div>
      </div>
    );
  }

  return (
    <div>
      <div className={styles.message}>{translate('NoSeriesFoundAddNew')}</div>

      <div className={styles.buttonContainer}>
        <Button to="/tv/add/new" kind={kinds.PRIMARY}>
          {translate('AddNewSeries')}
        </Button>
      </div>
    </div>
  );
}

export default NoSeries;
