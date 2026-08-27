# WP-008 Implementation Report

**Date:** 2026-08-27  
**Status:** `WP008_STATUS = BLOCKED`

## Execution checklist and delivered scope

- Read the approved architecture, authorization and WP-007 contracts; inspected the four requested legacy forms read-only.
- Added the production launcher, atomic create/start orchestration, frozen multi-eye workspace, keyboard save contract, visual/final lifecycle actions, scoped history, Admin correction and local static assets.
- Added the append-only `MeasurementRevision`, forward Admin-only permission migration, centralized Decimal parser, documentation and CI static smoke entries.
- WP008-FIX-001 adds a protected frozen-requirement PDF overlay, server-result marker updates, measurement-group filtering, visual-row editing, mutation-aware navigation, filter-preserving history pagination, locked ACTIVE-revision validation, enriched correction audit metadata, and focused service/HTTP/static acceptance coverage.
- WP008-FINAL-001 isolates marker CSS from the WP-005 viewer, adds cross-page marker navigation and live mandatory progress, routes history-only active-session links to read-only detail, validates public query filters/UUIDs, rejects inactive locked Drawings, and adds final regression coverage.

## Architecture and evidence

The UI uses the explicit WP-007 service boundary and existing secure drawing viewer. Historical pages use frozen Measurement fields. Correction follows the legacy Admin-only intent evidenced by `FrmMeasurementCorrection.vb`, but replaces physical overwrite without trace with an append-only revision. `FrmMeasurementEntry.vb`, `FrmMeasurementHistory.vb`, and `FrmVisualControl.vb` were used only as interaction evidence; no WinForms persistence pattern was ported.

## Verification status

Python source compilation, whitespace validation, and the immutable legacy diff pass. Dependency installation was retried but the environment package tunnel returned HTTP 403, and Docker is unavailable, so Django checks, migration drift, clean PostgreSQL migration, pytest, image/static/private-volume gates and GitHub Actions cannot run locally. The expanded `test_wp008.py` suite contains focused HTTP/service/static contracts for the requested pilot, correction, and final hardening cases, but an actual passing test count and CI run cannot be claimed until GitHub executes them. Status therefore remains BLOCKED until the complete verify workflow is green; WP-009 was not started.

## Known gates

See `WP008_DEFERRED_DECISIONS.md`. In particular there is no ticket side effect, visual catalog, mold-derived eye count, SPC, migration or historical-limit editing.
