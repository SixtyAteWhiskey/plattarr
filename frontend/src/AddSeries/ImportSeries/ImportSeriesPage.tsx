import React from 'react';
import { Route } from 'react-router-dom';
import Switch from 'Components/Router/Switch';
import ImportSeries from './Import/ImportSeries';
import ImportSeriesSelectFolder from './SelectFolder/ImportSeriesSelectFolder';

function ImportSeriesPage() {
  return (
    <Switch>
      <Route
        exact={true}
        path="/tv/add/import"
        component={ImportSeriesSelectFolder}
      />

      <Route path="/tv/add/import/:rootFolderId" component={ImportSeries} />
    </Switch>
  );
}

export default ImportSeriesPage;
