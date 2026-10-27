import classNames from 'classnames';
import React, { HTMLAttributes } from 'react';
import { Kind } from 'Helpers/Props/kinds';
import styles from './Alert.css';

interface AlertProps extends HTMLAttributes<HTMLDivElement> {
  kind?: Extract<Kind, keyof typeof styles>;
  children: React.ReactNode;
}

function Alert(props: AlertProps) {
  const {
    className = styles.alert,
    kind = 'info',
    children,
    ...otherProps
  } = props;

  return (
    <div className={classNames(className, styles[kind])} {...otherProps}>
      {children}
    </div>
  );
}

export default Alert;
