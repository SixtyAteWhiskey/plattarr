import classNames from 'classnames';
import React, { ComponentPropsWithoutRef, ReactNode, useContext } from 'react';
import ColorImpairedContext from 'App/ColorImpairedContext';
import Icon, { IconName } from 'Components/Icon';
import { StatusTone } from './StatusTone';
import styles from './StatusBadge.css';
import toneStyles from './StatusTone.css';

export interface StatusBadgeProps
  extends Omit<ComponentPropsWithoutRef<'span'>, 'children'> {
  detail?: ReactNode;
  icon: IconName;
  label: ReactNode;
  tone: StatusTone;
}

export default function StatusBadge({
  className,
  detail,
  icon,
  label,
  tone,
  ...otherProps
}: StatusBadgeProps) {
  const colorImpairedMode = useContext(ColorImpairedContext);

  return (
    <span
      className={classNames(
        styles.badge,
        toneStyles[tone],
        colorImpairedMode && toneStyles.colorImpaired,
        className
      )}
      {...otherProps}
    >
      <Icon
        className={styles.icon}
        name={icon}
        size={12}
        aria-hidden={true}
        focusable={false}
      />

      <span>{label}</span>

      {detail !== undefined && detail !== null ? (
        <>
          <span className={styles.separator} aria-hidden={true}>
            ·
          </span>
          <span>{detail}</span>
        </>
      ) : null}
    </span>
  );
}
