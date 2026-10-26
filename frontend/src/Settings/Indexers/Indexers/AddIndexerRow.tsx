import React, { useCallback } from 'react';
import { useDispatch } from 'react-redux';
import ProtocolLabel from 'Activity/Queue/ProtocolLabel';
import Icon from 'Components/Icon';
import Label, { LabelProps } from 'Components/Label';
import TableRowCell from 'Components/Table/Cells/TableRowCell';
import TableRowButton from 'Components/Table/TableRowButton';
import { icons, kinds } from 'Helpers/Props';
import { selectIndexerSchema } from 'Store/Actions/settingsActions';
import Indexer, { IndexerPrivacy } from 'typings/Indexer';
import translate from 'Utilities/String/translate';
import getIndexerCategories from './getIndexerCategories';
import styles from './AddIndexerRow.css';

interface AddIndexerRowProps {
  indexer: Indexer;
  isAdded: boolean;
  onIndexerSelect: () => void;
}

function getPrivacyLabel(
  privacy?: IndexerPrivacy
): { kind: LabelProps['kind']; label: string } | null {
  switch (privacy) {
    case 'public':
      return { kind: kinds.DANGER, label: translate('Public') };
    case 'semiPrivate':
      return { kind: kinds.WARNING, label: translate('SemiPrivate') };
    case 'private':
      return { kind: kinds.INFO, label: translate('Private') };
    default:
      return null;
  }
}

function AddIndexerRow({
  indexer,
  isAdded,
  onIndexerSelect,
}: AddIndexerRowProps) {
  const dispatch = useDispatch();
  const { implementation, implementationName, name, protocol } = indexer;
  const privacy = getPrivacyLabel(indexer.privacy);
  const categories = getIndexerCategories(indexer);

  const handlePress = useCallback(() => {
    dispatch(
      selectIndexerSchema({
        implementation,
        implementationName,
        schemaName: name,
      })
    );

    onIndexerSelect();
  }, [implementation, implementationName, name, dispatch, onIndexerSelect]);

  return (
    <TableRowButton onPress={handlePress}>
      <TableRowCell className={styles.protocol}>
        <ProtocolLabel protocol={protocol} />
      </TableRowCell>

      <TableRowCell className={styles.name}>
        <div className={styles.nameContent}>
          <span>{name}</span>
          {isAdded && (
            <Label kind={kinds.SUCCESS}>
              <Icon name={icons.CHECK} size={10} /> {translate('Added')}
            </Label>
          )}
        </div>
      </TableRowCell>

      <TableRowCell className={styles.language}>
        {indexer.language}
      </TableRowCell>

      <TableRowCell className={styles.description}>
        {indexer.description}
      </TableRowCell>

      <TableRowCell className={styles.privacy}>
        {privacy ? <Label kind={privacy.kind}>{privacy.label}</Label> : null}
      </TableRowCell>

      <TableRowCell className={styles.categories}>
        <div className={styles.categoryList}>
          {categories.map((category) => (
            <Label key={category.id} kind={kinds.WARNING}>
              {category.name}
            </Label>
          ))}
        </div>
      </TableRowCell>
    </TableRowButton>
  );
}

export default AddIndexerRow;
