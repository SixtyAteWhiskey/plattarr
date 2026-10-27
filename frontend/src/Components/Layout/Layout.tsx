import classNames from 'classnames';
import React, { HTMLAttributes, ReactNode } from 'react';
import styles from './Layout.css';

export type LayoutComponent =
  | 'article'
  | 'div'
  | 'header'
  | 'ol'
  | 'section'
  | 'ul';
export type LayoutGap =
  | 'xSmall'
  | 'small'
  | 'medium'
  | 'large'
  | 'xLarge'
  | 'xxLarge';

export interface LayoutProps extends HTMLAttributes<HTMLElement> {
  children?: ReactNode;
  component?: LayoutComponent;
  gap?: LayoutGap;
}

const gapClassNames: Record<LayoutGap, string> = {
  xSmall: styles.gapXSmall,
  small: styles.gapSmall,
  medium: styles.gapMedium,
  large: styles.gapLarge,
  xLarge: styles.gapXLarge,
  xxLarge: styles.gapXXLarge,
};

export type StackProps = LayoutProps;

export function Stack({
  children,
  className,
  component: Component = 'div',
  gap = 'medium',
  ...otherProps
}: StackProps) {
  return (
    <Component
      className={classNames(styles.stack, gapClassNames[gap], className)}
      {...otherProps}
    >
      {children}
    </Component>
  );
}

export type ClusterAlignment =
  | 'start'
  | 'center'
  | 'end'
  | 'baseline'
  | 'stretch';
export type ClusterJustification = 'start' | 'center' | 'end' | 'between';

export interface ClusterProps extends LayoutProps {
  align?: ClusterAlignment;
  collapseOnSmall?: boolean;
  justify?: ClusterJustification;
}

const alignClassNames: Record<ClusterAlignment, string> = {
  start: styles.alignStart,
  center: styles.alignCenter,
  end: styles.alignEnd,
  baseline: styles.alignBaseline,
  stretch: styles.alignStretch,
};

const justifyClassNames: Record<ClusterJustification, string> = {
  start: styles.justifyStart,
  center: styles.justifyCenter,
  end: styles.justifyEnd,
  between: styles.justifyBetween,
};

export function Cluster({
  align = 'center',
  children,
  className,
  collapseOnSmall = false,
  component: Component = 'div',
  gap = 'small',
  justify = 'start',
  ...otherProps
}: ClusterProps) {
  return (
    <Component
      className={classNames(
        styles.cluster,
        gapClassNames[gap],
        alignClassNames[align],
        justifyClassNames[justify],
        collapseOnSmall && styles.collapseOnSmall,
        className
      )}
      {...otherProps}
    >
      {children}
    </Component>
  );
}

export type ResponsiveGridMinimum = 'compact' | 'standard' | 'wide';

export interface ResponsiveGridProps extends LayoutProps {
  minimum?: ResponsiveGridMinimum;
}

export function ResponsiveGrid({
  children,
  className,
  component: Component = 'div',
  gap = 'small',
  minimum = 'standard',
  ...otherProps
}: ResponsiveGridProps) {
  return (
    <Component
      className={classNames(
        styles.responsiveGrid,
        gapClassNames[gap],
        styles[minimum],
        className
      )}
      {...otherProps}
    >
      {children}
    </Component>
  );
}
