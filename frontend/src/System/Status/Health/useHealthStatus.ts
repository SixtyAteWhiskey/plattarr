import { useMemo } from 'react';
import { useSelector } from 'react-redux';
import createHealthSelector from './createHealthSelector';

function useHealthStatus() {
  const { isPopulated, items } = useSelector(createHealthSelector());

  return useMemo(() => {
    let errors = false;
    let warnings = false;

    items.forEach((item) => {
      if (item.type === 'error') {
        errors = true;
      }

      if (item.type === 'warning') {
        warnings = true;
      }
    });

    return {
      count: items.length,
      errors,
      isPopulated,
      warnings,
    };
  }, [isPopulated, items]);
}

export default useHealthStatus;
