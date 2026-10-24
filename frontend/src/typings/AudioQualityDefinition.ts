import ModelBase from 'App/ModelBase';

export default interface AudioQualityDefinition extends ModelBase {
  quality: {
    id: number;
    name: string;
  };
  title: string;
  weight: number;
  minSize?: number;
  maxSize?: number;
  preferredSize?: number;
}
