# Contributing

Plattarr is a personal project that combines movie, TV, and music workflows.
Small bug fixes and focused improvements are welcome. For larger changes,
please open an issue first so the approach can be discussed before substantial
work begins.

## Development setup

The repository pins its .NET SDK in `global.json`. Frontend builds use Node.js
24 and Yarn. On a fresh Linux environment, run:

```sh
tools/dev-setup.sh --frontend
```

To build and run the backend verification suite:

```sh
. tools/env.sh
tools/verify.sh
```

For frontend changes, run the relevant lint, type-check, and production build
commands documented in [`tools/README.md`](tools/README.md).

## Change guidelines

- Keep changes focused and follow the existing Movies, TV, and Music module
  boundaries.
- Add or update tests for behavior changes.
- When changing API resources or controllers, regenerate the OpenAPI document
  with `tools/genspec.sh` and review the diff.
- Add user-visible strings to the English localization file.
- Never include API keys, passwords, production database contents, or private
  host details in commits or issue reports.

## Pull requests

Include a short description of the change, how it was verified, and any
follow-up work. For UI changes, include screenshots when practical. Keep each
pull request focused on one feature or fix.
