import classNames from 'classnames';
import React, {
  ElementType,
  KeyboardEvent,
  ReactNode,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Manager, Popper, Reference } from 'react-popper';
import Icon from 'Components/Icon';
import Link from 'Components/Link/Link';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import Portal from 'Components/Portal';
import Scroller from 'Components/Scroller/Scroller';
import useMeasure from 'Helpers/Hooks/useMeasure';
import { icons } from 'Helpers/Props';
import ArrayElement from 'typings/Helpers/ArrayElement';
import { EnhancedSelectInputChanged, InputChanged } from 'typings/inputs';
import { isMobile as isMobileUtil } from 'Utilities/browser';
import * as keyCodes from 'Utilities/Constants/keyCodes';
import getUniqueElementId from 'Utilities/getUniqueElementId';
import translate from 'Utilities/String/translate';
import TextInput from '../TextInput';
import HintedSelectInputOption from './HintedSelectInputOption';
import HintedSelectInputSelectedValue from './HintedSelectInputSelectedValue';
import styles from './EnhancedSelectInput.css';

const MINIMUM_DISTANCE_FROM_EDGE = 10;

function isArrowKey(keyCode: number) {
  return keyCode === keyCodes.UP_ARROW || keyCode === keyCodes.DOWN_ARROW;
}

function getSelectedOption<T extends EnhancedSelectInputValue<V>, V>(
  selectedIndex: number,
  values: T[]
) {
  return values[selectedIndex];
}

function findIndex<T extends EnhancedSelectInputValue<V>, V>(
  startingIndex: number,
  direction: 1 | -1,
  values: T[]
) {
  if (!values.length) {
    return null;
  }

  let indexToTest = startingIndex + direction;

  for (let attempts = 0; attempts < values.length; attempts++) {
    if (indexToTest < 0) {
      indexToTest = values.length - 1;
    } else if (indexToTest >= values.length) {
      indexToTest = 0;
    }

    const option = getSelectedOption(indexToTest, values);
    if (!option.isDisabled && !option.isHidden) {
      return indexToTest;
    }

    indexToTest += direction;
  }

  return null;
}

function previousIndex<T extends EnhancedSelectInputValue<V>, V>(
  selectedIndex: number,
  values: T[]
) {
  return findIndex(selectedIndex, -1, values);
}

function nextIndex<T extends EnhancedSelectInputValue<V>, V>(
  selectedIndex: number,
  values: T[]
) {
  return findIndex(selectedIndex, 1, values);
}

function getSelectedIndex<T extends EnhancedSelectInputValue<V>, V>(
  value: V,
  values: T[]
) {
  if (Array.isArray(value)) {
    return values.findIndex((v) => {
      return v.key === value[0];
    });
  }

  return values.findIndex((v) => {
    return v.key === value;
  });
}

function isSelectedItem<T extends EnhancedSelectInputValue<V>, V>(
  index: number,
  value: V,
  values: T[]
) {
  if (Array.isArray(value)) {
    return value.includes(values[index].key);
  }

  return values[index].key === value;
}

export interface EnhancedSelectInputValue<V> {
  key: ArrayElement<V>;
  value: string;
  hint?: ReactNode;
  isDisabled?: boolean;
  isHidden?: boolean;
  parentKey?: V;
  additionalProperties?: object;
}

export interface EnhancedSelectInputProps<
  T extends EnhancedSelectInputValue<V>,
  V
> {
  id?: string;
  className?: string;
  disabledClassName?: string;
  name: string;
  value: V;
  values: T[];
  isDisabled?: boolean;
  isFetching?: boolean;
  isEditable?: boolean;
  hasError?: boolean;
  hasWarning?: boolean;
  valueOptions?: object;
  selectedValueOptions?: object;
  selectedValueComponent?: string | ElementType;
  optionComponent?: ElementType;
  onOpen?: () => void;
  onChange: (change: EnhancedSelectInputChanged<V>) => void;
}

function EnhancedSelectInput<T extends EnhancedSelectInputValue<V>, V>(
  props: EnhancedSelectInputProps<T, V>
) {
  const {
    id,
    className = styles.enhancedSelect,
    disabledClassName = styles.isDisabled,
    name,
    value,
    values,
    isDisabled = false,
    isEditable,
    isFetching,
    hasError,
    hasWarning,
    valueOptions,
    selectedValueOptions,
    selectedValueComponent:
      SelectedValueComponent = HintedSelectInputSelectedValue,
    optionComponent: OptionComponent = HintedSelectInputOption,
    onChange,
    onOpen,
  } = props;

  const [measureRef, { width }] = useMeasure();
  const updater = useRef<(() => void) | null>(null);
  const buttonId = useMemo(() => getUniqueElementId(), []);
  const optionsId = useMemo(() => getUniqueElementId(), []);
  const mobileOptionsId = useMemo(() => getUniqueElementId(), []);
  const [selectedIndex, setSelectedIndex] = useState(
    getSelectedIndex(value, values)
  );
  const [isOpen, setIsOpen] = useState(false);
  const isMobile = useMemo(() => isMobileUtil(), []);
  const wasOpen = useRef(isOpen);
  const focusReturnElement = useRef<HTMLElement | null>(null);

  const rememberFocusReturnElement = useCallback(() => {
    const activeElement = document.activeElement;

    if (
      activeElement instanceof HTMLElement &&
      activeElement !== document.body
    ) {
      focusReturnElement.current = activeElement;
    }
  }, []);

  useEffect(() => {
    if (isMobile && wasOpen.current && !isOpen) {
      const element = focusReturnElement.current;

      if (element?.isConnected) {
        window.requestAnimationFrame(() => element.focus());
      }
    }

    wasOpen.current = isOpen;
  }, [isMobile, isOpen]);

  const isMultiSelect = Array.isArray(value);
  const selectedOption = getSelectedOption(selectedIndex, values);
  const listboxId = isMobile ? mobileOptionsId : optionsId;
  const activeDescendantId =
    isOpen &&
    selectedIndex >= 0 &&
    selectedIndex < values.length &&
    !values[selectedIndex].isDisabled &&
    !values[selectedIndex].isHidden
      ? `${listboxId}-option-${selectedIndex}`
      : undefined;

  const selectedValue = useMemo(() => {
    if (values.length) {
      return value;
    }

    if (isMultiSelect) {
      return [];
    } else if (typeof value === 'number') {
      return 0;
    }

    return '';
  }, [value, values, isMultiSelect]);

  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const handleComputeMaxHeight = useCallback((data: any) => {
    const windowHeight = window.innerHeight;

    data.styles.maxHeight = windowHeight - MINIMUM_DISTANCE_FROM_EDGE;

    return data;
  }, []);

  const handleWindowClick = useCallback(
    (event: MouseEvent) => {
      const button = document.getElementById(buttonId);
      const options = document.getElementById(optionsId);
      const eventTarget = event.target as HTMLElement;

      if (!button || !eventTarget.isConnected || isMobile) {
        return;
      }

      if (
        !button.contains(eventTarget) &&
        options &&
        !options.contains(eventTarget) &&
        isOpen
      ) {
        setIsOpen(false);
        window.removeEventListener('click', handleWindowClick);
      }
    },
    [isMobile, isOpen, buttonId, optionsId, setIsOpen]
  );

  const addListener = useCallback(() => {
    window.addEventListener('click', handleWindowClick);
  }, [handleWindowClick]);

  const removeListener = useCallback(() => {
    window.removeEventListener('click', handleWindowClick);
  }, [handleWindowClick]);

  const handlePress = useCallback(() => {
    if (!isOpen && onOpen) {
      onOpen();
    }

    if (!isOpen && isMobile) {
      rememberFocusReturnElement();
    }

    setIsOpen(!isOpen);
  }, [isOpen, setIsOpen, onOpen, isMobile, rememberFocusReturnElement]);

  const handleSelect = useCallback(
    (newValue: ArrayElement<V>) => {
      const additionalProperties = values.find(
        (v) => v.key === newValue
      )?.additionalProperties;

      if (Array.isArray(value)) {
        const index = value.indexOf(newValue);

        if (index === -1) {
          const arrayValue = values
            .map((v) => v.key)
            .filter((v) => v === newValue || value.includes(v));

          onChange({
            name,
            value: arrayValue as V,
            additionalProperties,
          });
        } else {
          const arrayValue = [...value];
          arrayValue.splice(index, 1);

          onChange({
            name,
            value: arrayValue as V,
            additionalProperties,
          });
        }
      } else {
        setIsOpen(false);

        onChange({
          name,
          value: newValue as V,
          additionalProperties,
        });
      }
    },
    [name, value, values, onChange, setIsOpen]
  );

  const handleBlur = useCallback(() => {
    if (!isEditable) {
      // Calling setState without this check prevents the click event from being properly handled on Chrome (it is on firefox)
      const origIndex = getSelectedIndex(value, values);

      if (origIndex !== selectedIndex) {
        setSelectedIndex(origIndex);
      }
    }
  }, [value, values, isEditable, selectedIndex, setSelectedIndex]);

  const handleFocus = useCallback(() => {
    if (isOpen) {
      removeListener();
      setIsOpen(false);
    }
  }, [isOpen, setIsOpen, removeListener]);

  const handleKeyDown = useCallback(
    (event: KeyboardEvent<HTMLElement>) => {
      const keyCode = event.keyCode;
      let nextIsOpen: boolean | null = null;
      let nextSelectedIndex: number | null = null;

      if (!isOpen) {
        if (isArrowKey(keyCode)) {
          event.preventDefault();
          nextIsOpen = true;

          if (isMobile) {
            rememberFocusReturnElement();
          }
        }

        if (
          selectedIndex == null ||
          selectedIndex === -1 ||
          getSelectedOption(selectedIndex, values).isDisabled
        ) {
          if (keyCode === keyCodes.UP_ARROW) {
            nextSelectedIndex = previousIndex(0, values);
          } else if (keyCode === keyCodes.DOWN_ARROW) {
            nextSelectedIndex = nextIndex(values.length - 1, values);
          }
        }

        if (nextIsOpen !== null) {
          setIsOpen(nextIsOpen);
        }

        if (nextSelectedIndex !== null) {
          setSelectedIndex(nextSelectedIndex);
        }
        return;
      }

      if (keyCode === keyCodes.UP_ARROW) {
        event.preventDefault();
        nextSelectedIndex = previousIndex(selectedIndex, values);
      }

      if (keyCode === keyCodes.DOWN_ARROW) {
        event.preventDefault();
        nextSelectedIndex = nextIndex(selectedIndex, values);
      }

      if (keyCode === keyCodes.ENTER) {
        event.preventDefault();
        nextIsOpen = false;
        if (selectedIndex >= 0 && values[selectedIndex]) {
          handleSelect(values[selectedIndex].key);
        }
      }

      if (keyCode === keyCodes.TAB) {
        nextIsOpen = false;
        if (selectedIndex >= 0 && values[selectedIndex]) {
          handleSelect(values[selectedIndex].key);
        }

        if (
          isMobile &&
          event.currentTarget.getAttribute('role') === 'listbox'
        ) {
          event.preventDefault();

          const focusableElements = Array.from(
            document.querySelectorAll<HTMLElement>(
              'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
            )
          ).filter(
            (element) =>
              element.tabIndex >= 0 &&
              element.getClientRects().length > 0 &&
              !element.closest('[aria-hidden="true"]')
          );
          const currentFocusIndex = focusableElements.indexOf(
            focusReturnElement.current as HTMLElement
          );

          focusReturnElement.current =
            currentFocusIndex >= 0
              ? focusableElements[currentFocusIndex + 1] ?? null
              : null;
        }
      }

      if (keyCode === keyCodes.ESCAPE) {
        event.preventDefault();
        event.stopPropagation();
        nextIsOpen = false;
        nextSelectedIndex = getSelectedIndex(value, values);
      }

      if (nextIsOpen !== null) {
        setIsOpen(nextIsOpen);
      }

      if (nextSelectedIndex !== null) {
        setSelectedIndex(nextSelectedIndex);
      }
    },
    [
      value,
      isOpen,
      selectedIndex,
      values,
      setIsOpen,
      setSelectedIndex,
      handleSelect,
      isMobile,
      rememberFocusReturnElement,
    ]
  );

  const handleOptionsModalClose = useCallback(() => {
    setIsOpen(false);
  }, [setIsOpen]);

  const handleEditChange = useCallback(
    (change: InputChanged<string>) => {
      onChange(change as EnhancedSelectInputChanged<V>);
    },
    [onChange]
  );

  useEffect(() => {
    if (updater.current) {
      updater.current();
    }
  });

  useEffect(() => {
    if (isOpen) {
      addListener();
    } else {
      removeListener();
    }

    return removeListener;
  }, [isOpen, addListener, removeListener]);

  return (
    <div>
      <Manager>
        <Reference>
          {({ ref }) => (
            <div ref={ref} id={buttonId}>
              <div ref={measureRef}>
                {isEditable && typeof value === 'string' ? (
                  <div className={styles.editableContainer}>
                    <TextInput
                      id={id}
                      className={className}
                      name={name}
                      value={value}
                      readOnly={isDisabled}
                      hasError={hasError}
                      hasWarning={hasWarning}
                      role="combobox"
                      aria-haspopup="listbox"
                      aria-expanded={isOpen}
                      aria-controls={isOpen ? listboxId : undefined}
                      aria-activedescendant={activeDescendantId}
                      aria-labelledby={id ? `${id}-label` : undefined}
                      aria-autocomplete="list"
                      onFocus={handleFocus}
                      onBlur={handleBlur}
                      onKeyDown={handleKeyDown}
                      onChange={handleEditChange}
                    />
                    <Link
                      className={classNames(
                        styles.dropdownArrowContainerEditable,
                        isDisabled
                          ? styles.dropdownArrowContainerDisabled
                          : styles.dropdownArrowContainer
                      )}
                      onPress={handlePress}
                    >
                      {isFetching ? (
                        <LoadingIndicator
                          className={styles.loading}
                          size={20}
                        />
                      ) : null}

                      {isFetching ? null : <Icon name={icons.CARET_DOWN} />}
                    </Link>
                  </div>
                ) : (
                  <Link
                    id={id}
                    className={classNames(
                      className,
                      hasError && styles.hasError,
                      hasWarning && styles.hasWarning,
                      isDisabled && disabledClassName
                    )}
                    isDisabled={isDisabled}
                    role="combobox"
                    aria-haspopup="listbox"
                    aria-expanded={isOpen}
                    aria-controls={isOpen ? listboxId : undefined}
                    aria-activedescendant={activeDescendantId}
                    aria-labelledby={id ? `${id}-label` : undefined}
                    onBlur={handleBlur}
                    onKeyDown={handleKeyDown}
                    onPress={handlePress}
                  >
                    <SelectedValueComponent
                      values={values}
                      {...selectedValueOptions}
                      selectedValue={selectedValue}
                      isDisabled={isDisabled}
                      isMultiSelect={isMultiSelect}
                    >
                      {selectedOption ? selectedOption.value : selectedValue}
                    </SelectedValueComponent>

                    <div
                      className={
                        isDisabled
                          ? styles.dropdownArrowContainerDisabled
                          : styles.dropdownArrowContainer
                      }
                    >
                      {isFetching ? (
                        <LoadingIndicator
                          className={styles.loading}
                          size={20}
                        />
                      ) : null}

                      {isFetching ? null : <Icon name={icons.CARET_DOWN} />}
                    </div>
                  </Link>
                )}
              </div>
            </div>
          )}
        </Reference>
        <Portal>
          <Popper
            placement="bottom-start"
            modifiers={{
              computeMaxHeight: {
                order: 851,
                enabled: true,
                fn: handleComputeMaxHeight,
              },
              preventOverflow: {
                enabled: true,
                boundariesElement: 'viewport',
              },
            }}
          >
            {({ ref, style, scheduleUpdate }) => {
              updater.current = scheduleUpdate;

              return (
                <div
                  ref={ref}
                  id={optionsId}
                  role="listbox"
                  aria-hidden={!isOpen || isMobile}
                  aria-multiselectable={isMultiSelect || undefined}
                  className={styles.optionsContainer}
                  style={{
                    ...style,
                    minWidth: width,
                  }}
                >
                  {isOpen && !isMobile ? (
                    <Scroller
                      className={styles.options}
                      style={{
                        maxHeight: style.maxHeight,
                      }}
                    >
                      {values.map((v, index) => {
                        const hasParent = v.parentKey !== undefined;
                        const depth = hasParent ? 1 : 0;
                        const parentSelected =
                          v.parentKey !== undefined &&
                          Array.isArray(value) &&
                          value.includes(v.parentKey);

                        const { key, ...other } = v;

                        return (
                          <OptionComponent
                            key={v.key}
                            id={v.key}
                            optionId={`${optionsId}-option-${index}`}
                            depth={depth}
                            isSelected={isSelectedItem(index, value, values)}
                            isDisabled={!!v.isDisabled || parentSelected}
                            isMultiSelect={isMultiSelect}
                            {...valueOptions}
                            {...other}
                            isMobile={false}
                            onSelect={handleSelect}
                          >
                            {v.value}
                          </OptionComponent>
                        );
                      })}
                    </Scroller>
                  ) : null}
                </div>
              );
            }}
          </Popper>
        </Portal>
      </Manager>

      {isMobile ? (
        <Modal
          className={styles.optionsModal}
          size="extraSmall"
          isOpen={isOpen}
          onModalClose={handleOptionsModalClose}
        >
          <ModalBody
            className={styles.optionsModalBody}
            innerClassName={styles.optionsInnerModalBody}
            scrollDirection="none"
          >
            <div className={styles.mobileCloseButtonContainer}>
              <Link
                aria-label={translate('Close')}
                className={styles.mobileCloseButton}
                onPress={handleOptionsModalClose}
              >
                <Icon name={icons.CLOSE} size={18} />
              </Link>
            </div>

            <Scroller
              id={mobileOptionsId}
              role="listbox"
              aria-multiselectable={isMultiSelect || undefined}
              aria-activedescendant={activeDescendantId}
              className={styles.optionsModalScroller}
              autoFocus={true}
              tabIndex={0}
              onKeyDown={handleKeyDown}
            >
              {values.map((v, index) => {
                const hasParent = v.parentKey !== undefined;
                const depth = hasParent ? 1 : 0;
                const parentSelected =
                  v.parentKey !== undefined &&
                  isMultiSelect &&
                  value.includes(v.parentKey);

                const { key, ...other } = v;

                return (
                  <OptionComponent
                    key={key}
                    id={key}
                    optionId={`${mobileOptionsId}-option-${index}`}
                    depth={depth}
                    isSelected={isSelectedItem(index, value, values)}
                    isMultiSelect={isMultiSelect}
                    isDisabled={!!v.isDisabled || parentSelected}
                    {...valueOptions}
                    {...other}
                    isMobile={true}
                    onSelect={handleSelect}
                  >
                    {v.value}
                  </OptionComponent>
                );
              })}
            </Scroller>
          </ModalBody>
        </Modal>
      ) : null}
    </div>
  );
}

export default EnhancedSelectInput;
