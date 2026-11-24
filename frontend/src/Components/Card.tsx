import React, { useId } from 'react';
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
  const contentId = useId();
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
          aria-labelledby={ariaLabel ? undefined : contentId}
          className={styles.underlay}
          onPress={onPress}
        />

        <div id={contentId} className={overlayClassName}>
          {children}
        </div>
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
