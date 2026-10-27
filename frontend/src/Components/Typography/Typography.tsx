import classNames from 'classnames';
import React, { HTMLAttributes, ReactNode } from 'react';
import styles from './Typography.css';

export type HeadingLevel = 1 | 2 | 3 | 4;
export type HeadingSize = 'page' | 'section' | 'card';

export interface HeadingProps extends HTMLAttributes<HTMLHeadingElement> {
  children?: ReactNode;
  level?: HeadingLevel;
  size?: HeadingSize;
}

function getDefaultHeadingSize(level: HeadingLevel): HeadingSize {
  if (level === 1) {
    return 'page';
  }

  if (level === 2) {
    return 'section';
  }

  return 'card';
}

export function Heading({
  children,
  className,
  level = 2,
  size = getDefaultHeadingSize(level),
  ...otherProps
}: HeadingProps) {
  const Component = `h${level}` as const;

  return (
    <Component
      className={classNames(styles.heading, styles[size], className)}
      {...otherProps}
    >
      {children}
    </Component>
  );
}

export type TextComponent = 'dd' | 'div' | 'p' | 'span' | 'time';
export type TextSize = 'small' | 'body' | 'lead' | 'eyebrow';
export type TextTone = 'default' | 'secondary';
export type TextWeight = 'regular' | 'semibold';

export interface TextProps extends HTMLAttributes<HTMLElement> {
  children?: ReactNode;
  component?: TextComponent;
  size?: TextSize;
  tone?: TextTone;
  weight?: TextWeight;
}

export function Text({
  children,
  className,
  component: Component = 'p',
  size = 'body',
  tone = 'default',
  weight = 'regular',
  ...otherProps
}: TextProps) {
  return (
    <Component
      className={classNames(
        styles.text,
        styles[size],
        tone === 'secondary' && styles.secondary,
        styles[weight],
        className
      )}
      {...otherProps}
    >
      {children}
    </Component>
  );
}
