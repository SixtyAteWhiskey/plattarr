import AppSectionState from 'App/State/AppSectionState';
import TvInteractiveImport from 'InteractiveImport/Tv/TvInteractiveImport';

interface TvInteractiveImportAppState
  extends AppSectionState<TvInteractiveImport> {
  originalItems: TvInteractiveImport[];
}

export default TvInteractiveImportAppState;
