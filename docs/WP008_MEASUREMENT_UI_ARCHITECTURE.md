# WP-008 Measurement UI Architecture

## Execution checklist

- [x] Build a scoped launcher and atomic create/start orchestration.
- [x] Build a frozen-requirement, multi-eye, keyboard-first workspace.
- [x] Orchestrate the WP-007 measurement, visual, finalize and cancel commands.
- [x] Add scoped paginated history and snapshot-only detail.
- [x] Add append-only, Admin-only completed correction and aggregate recalculation.
- [x] Preserve local PDF.js/private drawing boundaries and add local assets.

## Boundaries

The HTML views orchestrate WP-007 commands; they do not assign inspection state or outcomes. The launch command nests draft creation and start in one transaction and accepts only an authorized ACTIVE revision. The workspace reads `InspectionRequirement.control_point_version`, never the current active definition. The drawing pane embeds the existing independently authorized secure viewer, so PDF bytes remain behind its private content endpoint.

Measurement saves address Session, Eye and Requirement UUIDs in the URL, accept only value and note, parse factory Decimal text centrally, and return the service-owned result. JavaScript advances on Enter only following an HTTP success and has an in-flight guard. PostgreSQL remains the reload source of truth.

History selectors scope every session query. Detail renders Measurement snapshots and appended revisions. Correction locks Session, Eye and Measurement, uses snapshot limits, preserves original measurement attribution/snapshots, appends a numbered revision, and recalculates only the completed session result. `measurements.correct` is scoped and granted solely to the business Admin role.
