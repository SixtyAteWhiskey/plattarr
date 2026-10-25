import DownloadProtocol from 'DownloadClient/DownloadProtocol';
import Provider from './Provider';

export type IndexerPrivacy = 'public' | 'semiPrivate' | 'private';

export interface IndexerCategory {
  id: number;
  name: string;
  subCategories: IndexerCategory[];
}

interface Indexer extends Provider {
  enableRss: boolean;
  enableAutomaticSearch: boolean;
  enableInteractiveSearch: boolean;
  supportsRss: boolean;
  supportsSearch: boolean;
  protocol: DownloadProtocol;
  priority: number;
  downloadClientId: number;
  tags: number[];
  description?: string;
  language?: string;
  privacy?: IndexerPrivacy;
  capabilities?: {
    categories: IndexerCategory[];
  };
}

export default Indexer;
