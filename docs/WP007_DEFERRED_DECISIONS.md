# WP-007 Deferred Decisions

WP-007 deliberately leaves these gates open rather than encoding speculative rules:

- Automatic Mold cavity derivation waits for an authoritative selected-mold/binding context; no arbitrary ProductMold or Mold FK is used.
- A required visual-control catalog/list is not normalized. WP-008 may present approved UX later; the core records an explicit visual-complete marker without guessing names/counts.
- `MeasurementRevision` and completed-record correction belong to WP-008 and its approval/audit decision.
- A proper metrology/device FK belongs to WP-011; no string placeholder exists.
- Quality→Production ticket creation belongs to WP-013 and can later key idempotently to a completed NOK session UUID.
- Production ticket auto-close waits for the actual ticket/binding integration.
- Commissioning linkage belongs to WP-022 and migration integration; no dangling UUID/FK exists.
- Legacy import and source-aware `RecordId` mapping belong to WP-009. Each legacy RecordId remains one imported session; no proximity-based heuristic merge is allowed even though new web sessions can have multiple eyes.
- No profiler-dependent uniqueness is imposed on measure codes, SPC keys, lot/serial or legacy IDs. Only frozen structural identity is constrained.
- No silent rounding/display policy is introduced. Values that cannot fit `numeric(14,5)` exactly are rejected.
