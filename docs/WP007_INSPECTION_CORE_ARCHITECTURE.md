# WP-007 Inspection Core Architecture

## Execution checklist

- [x] Read the approved source, authorization and WP-006 contracts and inspect legacy measurement evidence read-only.
- [x] Add the inspection aggregate, structural PostgreSQL constraints and generated schema migration.
- [x] Implement authorized transactional lifecycle services and authorized eager read selectors.
- [x] Cover definition freezing, multi-eye behavior, Decimal outcomes, snapshots, completeness, visual phase, finalization and cancellation.
- [x] Establish and regression-test one aggregate lock order and fresh-session lifecycle validation.
- [ ] Verify every CI gate and GitHub Actions on the existing PR.

## Aggregate and lifecycle

`InspectionSession` is rooted at an exact `DrawingRevision` and snapshots its Drawing scope. A DRAFT contains operator/lot/serial/declared-eye intent only; it has no requirements, eyes or measurements. `start_inspection` locks the session and atomically moves DRAFT to IN_PROGRESS, freezes the exact active definitions, creates deterministic eyes `1..declared_eye_count`, timestamps and audits. The only forward path is `DRAFT → IN_PROGRESS → WAITING_VISUAL → COMPLETED`. DRAFT, IN_PROGRESS and WAITING_VISUAL can become CANCELLED; terminal records cannot reopen or be changed through ordinary services.

`InspectionRequirement` is a technical freeze row pointing to the exact `ControlPointVersion` selected through WP-006's `list_active_versions_for_revision`. A started inspection never re-evaluates current active definitions. Consequently later authoring revisions cannot rewrite applicability or measurement snapshots.

## Transaction lock order

Every aggregate mutation acquires PostgreSQL row locks in one global order: `InspectionSession → InspectionEye → InspectionRequirement / Measurement / VisualControl`. Child-targeted commands perform an unlocked identity lookup only to resolve the authoritative parent UUID, then lock the current Session before locking and re-resolving the child under that Session. Lifecycle and authorization checks always use that freshly locked Session—not a possibly stale `select_related` object held by the caller. This ordering prevents the Eye→Session versus Session→Eye deadlock pattern, while the current-state check rejects a measurement or visual request that waited behind cancellation/completion.

## Eyes and closed-eye semantics

One session deliberately owns multiple `InspectionEye` rows. Eye count is explicit, defaults to one, and is not guessed from Product–Mold links. An eye may be closed only during IN_PROGRESS and only while it has no measurement or visual data. Closure retains actor/time/reason, is never physical deletion, prevents later measured/visual writes, and exempts that eye from mandatory and visual completion.

## Decimal engine and snapshots

Quality inputs and definition values remain Python `Decimal` / PostgreSQL `numeric(14,5)`. Services reject non-Decimal, non-finite, excess-scale and excess-integer values rather than rounding. The sole outcome rule is inclusive: `lower_limit <= measured_value <= upper_limit` is OK; otherwise NOK. ERROR is schema compatibility only and ordinary valid writes never create it.

The unique `(eye, requirement)` row is editable only in IN_PROGRESS. Its first write copies code, name, group, frequency, critical flag, sort, nominal, limits, unit, page, normalized coordinates, logical SPC key and version number from the frozen version. Draft updates recalculate value/result/actor/time/note but deliberately preserve that original definition snapshot.

## Completeness, visual phase and result

`finish_measurement_phase` locks the session and verifies every mandatory frozen requirement for every open eye; optional points may be absent. Success locks ordinary measurement edits by moving to WAITING_VISUAL. Visual records use UUID identity, arbitrary nonblank names and OK/NOK; no catalog or name uniqueness is inferred. An explicit eye marker completes visual work and is deliberately idempotent. It does not guess a required row count.

Finalization rechecks mandatory completeness, requires the visual marker for each open eye, and verifies closed eyes contain no data. Any NOK measurement or visual row produces overall NOK. Otherwise at least one open eye produces OK; an all-closed session produces NULL. Completion is timestamped and audited in the same transaction. No ticket side effect exists in WP-007.

## Authorization and selectors

Every mutation uses existing scoped `measurements.create` with `scope_type=DRAWING` and the authoritative Drawing scope. Reads use scoped `measurements.view_history`. Drawing visibility alone never implies measurement mutation. Public selectors cover session get/list, frozen requirements, eyes, measurements and visual controls with deterministic ordering and eager relationships to avoid routine N+1 access.

## WP-008 public contract

WP-008 must orchestrate, not reproduce ORM rules. Its command boundary is `create_inspection_draft`, `start_inspection`, `close_inspection_eye`, `save_measurement`, `finish_measurement_phase`, `create_visual_control`, `update_visual_control`, `complete_eye_visual_phase`, `finalize_inspection`, and `cancel_inspection`. Its query boundary is the six selectors in `apps.inspections.selectors`. Completed correction is not part of this contract yet.
