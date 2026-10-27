# Plattarr UI primitives

This foundation keeps shared presentation decisions consistent without moving
product rules into generic components. Hygiene is the first reference
implementation.

## Design tokens

Build-time tokens live in
`frontend/src/Styles/Variables/designSystem.js` and are available in CSS
modules through the PostCSS configuration.

- Typography: page, section, card, body, metadata, and eyebrow sizes; heading
  and body line heights; regular and semibold weights.
- Spacing: 4, 8, 12, 16, 24, 32, and 48 pixels.
- Shape: small and medium radii plus a pill radius.
- Surfaces: small, medium, and large padding.
- Responsive layout: compact, standard, and wide minimum column sizes.

Theme-dependent values live in both `Styles/Themes/light.js` and `dark.js`:
surface backgrounds, borders and shadows, accessible secondary text, and focus
ring colors, width, and offset. Meaningful small text must use
`secondaryTextColor`, not the lower-contrast help-text color.

## Status

```tsx
import { StatusBadge } from "Components/Status";

<StatusBadge
  icon={icons.WARNING}
  label="Warning"
  detail="Client unavailable"
  tone="warning"
/>;
```

Available tones are `danger`, `warning`, `monitor`, `info`, `neutral`, and
`success`. `StatusBadge` always requires a visible label and icon. It consumes
the color-impaired preference itself and adds border-style cues, so consumers
must not create a color-only status.

Feature code owns the mapping from domain state to tone. For example, Hygiene
maps stalled and slow downloads to `monitor`; the shared component does not
know reason codes or action eligibility.

Queue keeps its state mapping in `Activity/Queue/QueueStatus.tsx`. Errors and
failed processing use `danger`; client/import warnings use `warning`; active,
queued, and pending work use `info`; paused work uses `neutral`; and completed
downloads use `success`. The Queue table uses labeled badges, while compact
media-card consumers retain the icon treatment. The badge popover is keyboard
operable with Enter/Space and Escape, but it is supplemental: the visible badge
and accessible name carry the state without requiring hover.

System Health keeps its mapping in `System/Status/Health/Health.tsx`: errors,
warnings, notices, and unknown values map to `danger`, `warning`, `info`, and
`neutral`. Healthy configuration feedback combines a success badge with a
success-tinted surface. Health action routes and test commands remain separate
from presentation.

## Surfaces

```tsx
import Surface from "Components/Surface";

<Surface component="article" elevation="low" tone="danger">
  ...
</Surface>;
```

`Surface` is a noninteractive container. It supports `default`, `subtle`, and
`tinted` variants; `none`, `small`, `medium`, and `large` padding; and optional
low elevation or status tone. A tone adds a leading rail and a pattern in
color-impaired mode.

Do not use the existing `Card` as a neutral container: `Card` is an interactive
link or button. Conversely, do not put click behavior on `Surface`; use the
appropriate link or button component.

The Settings landing keeps each `Surface` presentational inside one block
`Link`, so the title and summary form a single focusable destination without
giving the surface button semantics.

## Layout

```tsx
import { Cluster, ResponsiveGrid, Stack } from 'Components/Layout';

<Stack gap="large">...</Stack>
<Cluster gap="small" justify="between" collapseOnSmall={true}>...</Cluster>
<ResponsiveGrid minimum="standard" gap="small">...</ResponsiveGrid>
```

- `Stack` arranges content vertically using the shared spacing scale.
- `Cluster` wraps related inline content and can stack at the existing small
  breakpoint.
- `ResponsiveGrid` uses an overflow-safe minimum column calculation and
  becomes one column at the existing small breakpoint.

These primitives compose inside the app's current page and scroll models. They
do not change the 768-pixel small-screen page-scroll switch, the 992-pixel
table overflow behavior, or virtualized media indexes.

Keep current settings forms in a single-column `Stack`: their label/control
rows switch layout at 1200 pixels and are not yet container-responsive. The UI
Settings reference form uses one surface per fieldset rather than placing form
sections in a `ResponsiveGrid`.

## Page hierarchy

`PageContent` sets the document title but does not render a visible page
heading. Each top-level route should add exactly one `Heading` at level 1 near
the start of `PageContentBody`, normally followed by a concise secondary lead.
Keep any `PageToolbar` as a direct sibling before `PageContentBody`; shared
presentation must not own product actions.

Compose the body by content type:

- Landing pages use an H1/lead header, H2 groups, H3 card titles, and a
  `ResponsiveGrid` of destination surfaces.
- Forms use an H1/lead header followed by one neutral surface per semantic
  fieldset in a single-column `Stack`.
- Tables and operational lists use an H1/lead header before feedback and the
  existing table or list. Do not wrap a table mechanically when doing so would
  interfere with its horizontal scroller.
- Status and detail pages use an H1/lead header followed by clearly separated
  neutral section surfaces. Domain status may add a tone inside those sections.

Loading, error, and empty feedback follows the page introduction and precedes
the primary content. Only new transactional or load failures use
`role="alert"`; persistent guidance and empty states remain non-live.

## Typography

```tsx
import { Heading, Text } from 'Components/Typography';

<Heading level={1}>Hygiene</Heading>
<Text size="small" tone="secondary">Movie · Download client</Text>
```

`Heading` provides page, section, and card sizes while preserving semantic
heading levels. `Text` provides body, lead, small, and eyebrow sizes; default
or secondary tone; and regular or semibold weight. Prefer semantic levels and
elements over selecting a size to imitate another element.

## Focus and alerts

Keyboard focus uses a theme-specific `:focus-visible` ring with shared width
and offset. Pointer focus remains quiet. Shared links, inputs, checkboxes,
tag/key-value inputs, and the header search preserve that contract.

`Alert` accepts native `div` attributes. Use `role="alert"` only for a new
transactional failure that needs immediate announcement. Informational,
loading, and persistent status content should not become live regions.

Queue and Health load failures use `role="alert"`. Queue empty/filter-empty,
Health success, and the persistent Health guidance remain non-live feedback.

## Adoption checklist

For each bounded migration:

1. Keep domain state and action eligibility in feature code.
2. Check light, dark, and color-impaired modes.
3. Check keyboard and pointer focus separately.
4. Check the relevant breakpoint boundaries and 480-pixel layout.
5. Confirm no document-level horizontal overflow or clipped focus ring.
6. Exercise read-only views first; do not broaden the migration into data or
   action changes.
