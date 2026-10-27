import classNames from 'classnames';
import React, { HTMLAttributes, ReactNode, useContext } from 'react';
import ColorImpairedContext from 'App/ColorImpairedContext';
import { StatusTone } from 'Components/Status/StatusTone';
import toneStyles from 'Components/Status/StatusTone.css';
import styles from './Surface.css';

export type SurfaceComponent = 'article' | 'div' | 'li' | 'section';
export type SurfaceElevation = 'none' | 'low';
export type SurfacePadding = 'none' | 'small' | 'medium' | 'large';
export type SurfaceVariant = 'default' | 'subtle' | 'tinted';

export interface SurfaceProps extends HTMLAttributes<HTMLElement> {
  children?: ReactNode;
  component?: SurfaceComponent;
  elevation?: SurfaceElevation;
  padding?: SurfacePadding;
  tone?: StatusTone;
  variant?: SurfaceVariant;
}

const paddingClassNames: Record<SurfacePadding, string> = {
  none: styles.paddingNone,
  small: styles.paddingSmall,
  medium: styles.paddingMedium,
  large: styles.paddingLarge,
};

export default function Surface({
  children,
  className,
  component: Component = 'div',
  elevation = 'none',
  padding = 'medium',
  tone,
  variant = 'default',
  ...otherProps
}: SurfaceProps) {
  const colorImpairedMode = useContext(ColorImpairedContext);

  return (
    <Component
      className={classNames(
        styles.surface,
        styles[variant],
        paddingClassNames[padding],
        elevation === 'low' && styles.elevationLow,
        tone && styles.tone,
        tone && toneStyles[tone],
        tone && colorImpairedMode && styles.colorImpaired,
        tone && colorImpairedMode && toneStyles.colorImpaired,
        className
      )}
      {...otherProps}
    >
      {children}
    </Component>
  );
}
