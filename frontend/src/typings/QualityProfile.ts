import Quality from 'Quality/Quality';
import { QualityProfileFormatItem } from './CustomFormat';
import MediaKind from './MediaKind';

export interface QualityProfileQualityItem {
  id?: number;
  quality?: Quality;
  items: QualityProfileQualityItem[];
  allowed: boolean;
  name?: string;
}

interface QualityProfile {
  name: string;
  appliesTo?: MediaKind[];
  upgradeAllowed: boolean;
  cutoff: number;
  items: QualityProfileQualityItem[];
  minFormatScore: number;
  cutoffFormatScore: number;
  minUpgradeFormatScore: number;
  formatItems: QualityProfileFormatItem[];
  id: number;
}

export default QualityProfile;
