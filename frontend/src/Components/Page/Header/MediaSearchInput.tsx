import { push } from 'connected-react-router';
import { ExtendedKeyboardEvent } from 'mousetrap';
import React, {
  FormEvent,
  KeyboardEvent,
  SyntheticEvent,
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import Autosuggest from 'react-autosuggest';
import { useDispatch, useSelector } from 'react-redux';
import { createSelector } from 'reselect';
import { useDebouncedCallback } from 'use-debounce';
import { Tag } from 'App/State/TagsAppState';
import Icon from 'Components/Icon';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import useKeyboardShortcuts from 'Helpers/Hooks/useKeyboardShortcuts';
import { icons } from 'Helpers/Props';
import Movie from 'Movie/Movie';
import Series from 'Series/Series';
import createAllMoviesSelector from 'Store/Selectors/createAllMoviesSelector';
import createAllSeriesSelector from 'Store/Selectors/createAllSeriesSelector';
import createDeepEqualSelector from 'Store/Selectors/createDeepEqualSelector';
import createTagsSelector from 'Store/Selectors/createTagsSelector';
import MediaKind from 'typings/MediaKind';
import translate from 'Utilities/String/translate';
import MediaSearchResult from './MediaSearchResult';
import styles from './MediaSearchInput.css';

const ADD_NEW_TYPE = 'addNew';
const ADD_NEW_KINDS: MediaKind[] = ['movies', 'tv'];

interface Match {
  key: string;
  refIndex: number;
}

interface AddNewSuggestion {
  type: 'addNew';
  kind: MediaKind;
  title: string;
}

// One entry of the combined library list the search runs over. Movies and
// series share a list so one search box finds either.
export interface SuggestedMedia
  extends Pick<Movie, 'title' | 'year' | 'titleSlug' | 'sortTitle' | 'imdbId'> {
  kind: MediaKind;
  images: Movie['images'] | Series['images'];
  originalTitle?: string;
  alternateTitles: { title: string }[];
  tmdbId?: number;
  tvdbId?: number;
  firstCharacter: string;
  tags: Tag[];
}

interface MediaSuggestion {
  title: string;
  indices: number[];
  item: SuggestedMedia;
  matches: Match[];
  refIndex: number;
}

interface Section {
  title: string;
  loading?: boolean;
  suggestions: MediaSuggestion[] | AddNewSuggestion[];
}

function getMediaPath(kind: MediaKind, titleSlug: string) {
  return `${window.Radarr.urlBase}/${
    kind === 'tv' ? 'series' : 'movie'
  }/${titleSlug}`;
}

function getAddNewPath(kind: MediaKind, term: string) {
  return `${window.Radarr.urlBase}/${
    kind === 'tv' ? 'tv/add/new' : 'add/new'
  }?term=${encodeURIComponent(term)}`;
}

function resolveTags(tagIds: number[], allTags: Tag[]) {
  return tagIds.reduce<Tag[]>((acc, id) => {
    const matchingTag = allTags.find((tag) => tag.id === id);

    if (matchingTag) {
      acc.push(matchingTag);
    }

    return acc;
  }, []);
}

function createUnoptimizedSelector() {
  return createSelector(
    createAllMoviesSelector(),
    createAllSeriesSelector(),
    createTagsSelector(),
    (allMovies, allSeries, allTags) => {
      const movies = allMovies.map((movie): SuggestedMedia => {
        const {
          title,
          originalTitle,
          year,
          titleSlug,
          sortTitle,
          images,
          alternateTitles = [],
          tmdbId,
          imdbId,
          tags = [],
        } = movie;

        return {
          kind: 'movies',
          title,
          originalTitle,
          year,
          titleSlug,
          sortTitle,
          images,
          alternateTitles,
          tmdbId,
          imdbId,
          firstCharacter: title.charAt(0).toLowerCase(),
          tags: resolveTags(tags, allTags),
        };
      });

      const series = allSeries.map((item): SuggestedMedia => {
        const {
          title,
          year,
          titleSlug,
          sortTitle,
          images,
          alternateTitles = [],
          tvdbId,
          imdbId,
          tags = [],
        } = item;

        return {
          kind: 'tv',
          title,
          year,
          titleSlug,
          sortTitle,
          images,
          alternateTitles,
          tvdbId,
          imdbId,
          firstCharacter: title.charAt(0).toLowerCase(),
          tags: resolveTags(tags, allTags),
        };
      });

      return [...movies, ...series];
    }
  );
}

function createMediaSelector() {
  return createDeepEqualSelector(createUnoptimizedSelector(), (media) => media);
}

function MediaSearchInput() {
  const media = useSelector(createMediaSelector());
  const dispatch = useDispatch();
  const { bindShortcut, unbindShortcut } = useKeyboardShortcuts();

  const [value, setValue] = useState('');
  const [requestLoading, setRequestLoading] = useState(false);
  const [suggestions, setSuggestions] = useState<MediaSuggestion[]>([]);

  const autosuggestRef = useRef<Autosuggest>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const worker = useRef<Worker | null>(null);
  const isLoading = useRef(false);
  const requestValue = useRef<string | null>(null);

  const suggestionGroups = useMemo(() => {
    const result: Section[] = [];

    if (suggestions.length || isLoading.current) {
      result.push({
        title: translate('ExistingMedia'),
        loading: isLoading.current,
        suggestions,
      });
    }

    result.push({
      title: translate('AddNew'),
      suggestions: [
        {
          type: ADD_NEW_TYPE,
          kind: 'movies',
          title: value,
        },
        {
          type: ADD_NEW_TYPE,
          kind: 'tv',
          title: value,
        },
      ],
    });

    return result;
  }, [suggestions, value]);

  const handleSuggestionsReceived = useCallback(
    (message: { data: { value: string; suggestions: MediaSuggestion[] } }) => {
      const { value, suggestions } = message.data;

      if (!isLoading.current) {
        requestValue.current = null;
        setRequestLoading(false);
      } else if (value === requestValue.current) {
        setSuggestions(suggestions);
        requestValue.current = null;
        setRequestLoading(false);
        isLoading.current = false;
        // setLoading(false);
      } else {
        setSuggestions(suggestions);
        setRequestLoading(true);

        const payload = {
          value: requestValue,
          media,
        };

        worker.current?.postMessage(payload);
      }
    },
    [media]
  );

  const requestSuggestions = useDebouncedCallback((value: string) => {
    if (!isLoading.current) {
      return;
    }

    requestValue.current = value;
    setRequestLoading(true);

    if (!requestLoading) {
      const payload = {
        value,
        media,
      };

      worker.current?.postMessage(payload);
    }
  }, 250);

  const reset = useCallback(() => {
    setValue('');
    setSuggestions([]);
    // setLoading(false);
    isLoading.current = false;
  }, []);

  const focusInput = useCallback((event: ExtendedKeyboardEvent) => {
    event.preventDefault();
    inputRef.current?.focus();
  }, []);

  const getSectionSuggestions = useCallback((section: Section) => {
    return section.suggestions;
  }, []);

  const renderSectionTitle = useCallback((section: Section) => {
    return (
      <div className={styles.sectionTitle}>
        {section.title}

        {section.loading && (
          <LoadingIndicator
            className={styles.loading}
            rippleClassName={styles.ripple}
            size={20}
          />
        )}
      </div>
    );
  }, []);

  const getSuggestionValue = useCallback(({ title }: { title: string }) => {
    return title;
  }, []);

  const renderSuggestion = useCallback(
    (
      item: AddNewSuggestion | MediaSuggestion,
      { query }: { query: string }
    ) => {
      if ('type' in item) {
        return (
          <div className={styles.addNewSuggestion}>
            <span className={styles.kind}>
              {item.kind === 'tv' ? translate('Series') : translate('Movie')}
            </span>

            {translate('SearchForQuery', { query })}
          </div>
        );
      }

      return <MediaSearchResult {...item.item} match={item.matches[0]} />;
    },
    []
  );

  const handleChange = useCallback(
    (
      _event: FormEvent<HTMLElement>,
      {
        newValue,
        method,
      }: {
        newValue: string;
        method: 'down' | 'up' | 'escape' | 'enter' | 'click' | 'type';
      }
    ) => {
      if (method === 'up' || method === 'down') {
        return;
      }

      setValue(newValue);
    },
    []
  );

  const handleKeyDown = useCallback(
    (event: KeyboardEvent<HTMLElement>) => {
      if (event.shiftKey || event.altKey || event.ctrlKey) {
        return;
      }

      if (event.key === 'Escape') {
        reset();
        return;
      }

      if (event.key !== 'Tab' && event.key !== 'Enter') {
        return;
      }

      if (!autosuggestRef.current) {
        return;
      }

      const { highlightedSectionIndex, highlightedSuggestionIndex } =
        autosuggestRef.current.state;

      const addNewSectionIndex = suggestionGroups.length - 1;

      if (!suggestions.length || highlightedSectionIndex) {
        // Enter on an "Add New" entry follows that entry; with nothing
        // highlighted the first entry (movies) is used.
        const addNewKind =
          highlightedSectionIndex === addNewSectionIndex &&
          highlightedSuggestionIndex != null
            ? ADD_NEW_KINDS[highlightedSuggestionIndex]
            : ADD_NEW_KINDS[0];

        dispatch(push(getAddNewPath(addNewKind, value)));

        inputRef.current?.blur();
        reset();

        return;
      }

      // If a suggestion is not selected go to the first match,
      // otherwise go to the selected match.

      const selectedSuggestion =
        highlightedSuggestionIndex == null
          ? suggestions[0]
          : suggestions[highlightedSuggestionIndex];

      dispatch(
        push(
          getMediaPath(
            selectedSuggestion.item.kind,
            selectedSuggestion.item.titleSlug
          )
        )
      );

      inputRef.current?.blur();
      reset();
    },
    [value, suggestions, suggestionGroups, dispatch, reset]
  );

  const handleBlur = useCallback(() => {
    reset();
  }, [reset]);

  const handleSuggestionsFetchRequested = useCallback(
    ({ value }: { value: string }) => {
      isLoading.current = true;

      requestSuggestions(value);
    },
    [requestSuggestions]
  );

  const handleSuggestionsClearRequested = useCallback(() => {
    setSuggestions([]);
    isLoading.current = false;
  }, []);

  const handleSuggestionSelected = useCallback(
    (
      _event: SyntheticEvent,
      { suggestion }: { suggestion: MediaSuggestion | AddNewSuggestion }
    ) => {
      if ('type' in suggestion) {
        dispatch(push(getAddNewPath(suggestion.kind, value)));
      } else {
        setValue('');
        dispatch(
          push(getMediaPath(suggestion.item.kind, suggestion.item.titleSlug))
        );
      }
    },
    [value, dispatch]
  );

  const inputProps = {
    ref: inputRef,
    className: styles.input,
    name: 'mediaSearch',
    value,
    placeholder: translate('Search'),
    autoComplete: 'off',
    spellCheck: false,
    onChange: handleChange,
    onKeyDown: handleKeyDown,
    onBlur: handleBlur,
  };

  const theme = {
    container: styles.container,
    containerOpen: styles.containerOpen,
    suggestionsContainer: styles.mediaContainer,
    suggestionsList: styles.list,
    suggestion: styles.listItem,
    suggestionHighlighted: styles.highlighted,
  };

  useEffect(() => {
    worker.current = new Worker(new URL('./fuse.worker.ts', import.meta.url));

    return () => {
      if (worker.current) {
        worker.current.terminate();
        worker.current = null;
      }
    };
  }, []);

  useEffect(() => {
    worker.current?.addEventListener(
      'message',
      handleSuggestionsReceived,
      false
    );

    return () => {
      if (worker.current) {
        worker.current.removeEventListener(
          'message',
          handleSuggestionsReceived,
          false
        );
      }
    };
  }, [handleSuggestionsReceived]);

  useEffect(() => {
    bindShortcut('focusMovieSearchInput', focusInput);

    return () => {
      unbindShortcut('focusMovieSearchInput');
    };
  }, [bindShortcut, unbindShortcut, focusInput]);

  return (
    <div className={styles.wrapper}>
      <Icon name={icons.SEARCH} />

      <Autosuggest
        ref={autosuggestRef}
        inputProps={inputProps}
        theme={theme}
        focusInputOnSuggestionClick={false}
        multiSection={true}
        suggestions={suggestionGroups}
        getSectionSuggestions={getSectionSuggestions}
        renderSectionTitle={renderSectionTitle}
        getSuggestionValue={getSuggestionValue}
        renderSuggestion={renderSuggestion}
        onSuggestionSelected={handleSuggestionSelected}
        onSuggestionsFetchRequested={handleSuggestionsFetchRequested}
        onSuggestionsClearRequested={handleSuggestionsClearRequested}
      />
    </div>
  );
}

export default MediaSearchInput;
