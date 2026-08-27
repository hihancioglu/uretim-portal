# WP-007 Implementation Report

**Date:** 2026-08-27
**Status:** `WP007_STATUS = BLOCKED`

## Delivered

Implemented the focused `apps.inspections` backend foundation: UUID session/requirement/eye/measurement/visual models, the approved five-state lifecycle, exact WP-006 definition freezing, deterministic multi-eye generation, closed-eye retention, Decimal-only inclusive OK/NOK calculation, complete server-owned snapshots, draft upsert behavior, mandatory completeness, explicit visual completion, final result aggregation, cancellation metadata, append-only transition audit, scoped WP-002 authorization, read-only Admin, and eager deterministic history selectors.

The migration contains only WP-007 structural constraints. It adds no ticket, production ticket, mold binding, commissioning, device, SPC, importer, correction model, visual catalog, guessed legacy relationship or speculative business uniqueness. No UI or WP-008 workflow was started.

## Evidence and tests

The service behavior follows `DOMAIN_RULES_V1.md` INS-001–INS-014 and the requested approved WP-007 decisions. Legacy measurement evidence was inspected under `legacy/TeknikResimOlcum` without modification; it remains AS-IS evidence rather than a target schema.

Automated WP-007 coverage includes inclusive Decimal boundaries and exact-representation rejection; empty/atomic start; definition freezing; deterministic eyes; snapshot integrity; draft updates; mandatory/optional completeness; closed/all-closed behavior; visual finalization; overall NOK; cancellation guards/audit; Manager/Technical Drawing denial and Incoming/Plastic scope separation using the real seeded WP-002 authorization layer.

WP007-FIX-001 corrected the 30 mm control-point fixture without weakening the approved definition constraints. All child-targeted services now obey the global `Session → Eye → Requirement/Measurement/VisualControl` lock order and validate lifecycle state from the freshly locked Session. Regression coverage now explicitly exercises competing starts, stale measurement/visual requests after cancellation, same-logical-point WP-006 revision freezing, every measurement snapshot field, close-with-data rejection, closed-eye visual rejection, missing visual completion, visual NOK aggregation/update locking, all mutable cancellation paths, cancelled-state write rejection, and the all-closed NULL result.

## Verification and exact blocker

Local source compilation passed. Dependency installation is unavailable because the environment package proxy returns HTTP 403, the checkout has no installed Django, and Docker is unavailable. Therefore Django migration generation/check, clean PostgreSQL migration, pytest, images/smokes and GitHub CI cannot be truthfully marked green locally. The PR must remain BLOCKED until GitHub Actions runs all preserved gates successfully; no existing CI gate was changed or weakened.

## Deferred gates

See `WP007_DEFERRED_DECISIONS.md`. In particular, WP-008 correction/UI, WP-009 import mapping, WP-011 metrology, WP-013 tickets, ticket auto-close and WP-022 commissioning remain separate work packages.
