import React from 'react';
import Link, { LinkProps } from 'Components/Link/Link';
import styles from './Card.css';

interface CardProps extends Pick<LinkProps, 'onPress'> {
  // TODO: Consider using different properties for classname depending if it's overlaying content or not
  className?: string;
  overlayClassName?: string;
  overlayContent?: boolean;
  'aria-label'?: string;
  children: React.ReactNode;
}

function Card(props: CardProps) {
  const {
    className = styles.card,
    overlayClassName = styles.overlay,
    overlayContent = false,
    children,
    onPress,
    'aria-label': ariaLabel,
  } = props;

  if (overlayContent) {
    return (
      <div className={className}>
        <Link
          aria-label={ariaLabel}
          className={styles.underlay}
          onPress={onPress}
        />

        <div className={overlayClassName}>{children}</div>
      </div>
    );
  }

  return (
    <Link aria-label={ariaLabel} className={className} onPress={onPress}>
      {children}
    </Link>
  );
}

export default Card;
