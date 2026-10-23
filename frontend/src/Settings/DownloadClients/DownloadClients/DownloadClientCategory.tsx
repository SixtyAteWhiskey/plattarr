import React from 'react';
import Label from 'Components/Label';
import { useMediaKind } from 'Helpers/mediaKindStore';
import {
  getClientCategory,
  ProviderFieldValue,
} from 'Helpers/moduleCategoryFields';
import { kinds } from 'Helpers/Props';
import translate from 'Utilities/String/translate';

interface DownloadClientCategoryProps {
  fields?: ProviderFieldValue[];
}

// The category this client sends the selected media type's downloads to.
function DownloadClientCategory({ fields }: DownloadClientCategoryProps) {
  const mediaKind = useMediaKind();
  const category = getClientCategory(fields, mediaKind);

  if (!category) {
    return null;
  }

  return (
    <Label kind={kinds.INFO}>
      {translate('Category')}: {category}
    </Label>
  );
}

export default DownloadClientCategory;
