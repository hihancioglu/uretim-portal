# WP-008 Implementation Report

**Date:** 2026-08-27  
**Status:** `WP008_STATUS = BLOCKED`

## Execution checklist and delivered scope

- Read the approved architecture, authorization and WP-007 contracts; inspected the four requested legacy forms read-only.
- Added the production launcher, atomic create/start orchestration, frozen multi-eye workspace, keyboard save contract, visual/final lifecycle actions, scoped history, Admin correction and local static assets.
- Added the append-only `MeasurementRevision`, forward Admin-only permission migration, centralized Decimal parser, documentation and CI static smoke entries.

## Architecture and evidence

The UI uses the explicit WP-007 service boundary and existing secure drawing viewer. Historical pages use frozen Measurement fields. Correction follows the legacy Admin-only intent evidenced by `FrmMeasurementCorrection.vb`, but replaces physical overwrite without trace with an append-only revision. `FrmMeasurementEntry.vb`, `FrmMeasurementHistory.vb`, and `FrmVisualControl.vb` were used only as interaction evidence; no WinForms persistence pattern was ported.

## Verification status

Python source compilation, whitespace validation, and the immutable legacy diff pass. Django checks, migration drift, clean PostgreSQL migration, pytest, Docker/static/private-volume gates and GitHub Actions cannot run locally because Django is not installed (`ModuleNotFoundError`). Status therefore remains BLOCKED until dependencies are available and the complete verify workflow is green. WP-009 was not started.

## Known gates

See `WP008_DEFERRED_DECISIONS.md`. In particular there is no ticket side effect, visual catalog, mold-derived eye count, SPC, migration or historical-limit editing.
