# WP-006 Implementation Report

**Date:** 2026-08-27  
**Status:** `WP006_STATUS = BLOCKED`

## Delivered

Added the focused `apps.control_points` domain with logical and versioned UUID models, PostgreSQL structural constraints, controlled Decimal parsing/calculation, stable SPC identity, per-revision active semantics, DRAFT/ACTIVE mutation policy, explicit transactional create/revise/deactivate/copy services, row locking, safe domain errors, same-transaction append-only audits, authorized reusable selectors, and inspection-only Admin registration.

Extended—rather than replaced—the WP-005 viewer. Its PDF.js 6.2.108/private Range pipeline remains unchanged. The existing overlay now loads active page-specific definitions, renders accessible normalized markers, captures CSS-plane click ratios, provides a no-write-until-save Turkish editor, supports deliberate reposition/revise, view-only details/history, and removes deactivated markers without deleting history. Management controls are conditional convenience only; endpoints independently enforce WP-002 scoped actions.

Migration `control_points/0001_initial.py` creates only WP-006 tables and structural lifecycle constraints. It intentionally contains neither Drawing/SPC nor revision/measure-code business uniqueness. No inspection, measurement, group-area editor, importer, decryption, PDF upgrade, SPA or DRF work was introduced.

## Tests and acceptance coverage

Tests cover comma/point Decimal parsing, malformed/excess precision rejection, canonical tolerance/limits, UUIDs, create/revise/revise/deactivate history, stable SPC key, copy identity/version/audit behavior, real Manager read/write separation, historical read access, lifecycle denial, structural uniqueness versus deferred business uniqueness, and static normalized-coordinate invariants. The deterministic scenario uses code `10`, name `Çap`, nominal `20`, tolerances `0,10/0,20`, then revises the name/location and deactivates.

## Verification and exact blocker

Local Python/Django commands cannot run because the checkout environment lacks Django (`ModuleNotFoundError: No module named 'django'`). Consequently the migration was authored to match model state after generation was attempted, and clean PostgreSQL migration, drift, full pytest and system-check gates await CI. Docker/CI/image/static/private-volume gates also remain unverified locally. WP-006 cannot truthfully be marked DONE until one completely green GitHub Actions verify run proves all required gates.

The legacy tree was not modified. See `WP006_DEFERRED_DECISIONS.md` for profiling, uniqueness, display rounding, comparison, group geometry and import gates.
