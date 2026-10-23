import Series, { SeriesMonitor, SeriesType } from 'Series/Series';
import { Error } from './AppSectionState';

export interface ImportSeries {
  id: string;
  error?: Error;
  isFetching: boolean;
  isPopulated: boolean;
  isQueued: boolean;
  items: Series[];
  monitor: SeriesMonitor;
  path: string;
  qualityProfileId: number;
  relativePath: string;
  seasonFolder: boolean;
  selectedSeries?: Series;
  seriesType: SeriesType;
  term: string;
}

interface ImportSeriesAppState {
  isLookingUpSeries: boolean;
  isImporting: boolean;
  isImported: boolean;
  importError: Error | null;
  items: ImportSeries[];
}

export default ImportSeriesAppState;
