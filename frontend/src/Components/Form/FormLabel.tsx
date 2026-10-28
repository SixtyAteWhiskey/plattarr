import classNames from 'classnames';
import React, { ReactNode } from 'react';
import { Size } from 'Helpers/Props/sizes';
import styles from './FormLabel.css';

interface FormLabelProps {
  children: ReactNode;
  className?: string;
  errorClassName?: string;
  size?: Extract<Size, keyof typeof styles>;
  component?: 'div' | 'label';
  name?: string;
  hasError?: boolean;
  isAdvanced?: boolean;
}

function FormLabel(props: FormLabelProps) {
  const {
    children,
    className = styles.label,
    errorClassName = styles.hasError,
    size = 'large',
    component = 'label',
    name,
    hasError,
    isAdvanced = false,
  } = props;

  const Component = component;

  return (
    <Component
      id={name ? `${name}-label` : undefined}
      className={classNames(
        className,
        styles[size],
        hasError && errorClassName,
        isAdvanced && styles.isAdvanced
      )}
      {...(component === 'label' ? { htmlFor: name } : {})}
    >
      {children}
    </Component>
  );
}

export default FormLabel;
