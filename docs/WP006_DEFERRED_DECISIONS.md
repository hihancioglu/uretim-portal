# WP-006 Deferred Decisions

- Authoritative/current WP-000 legacy profiling remains pending.
- Hard `Drawing + spc_key` uniqueness remains deferred; UUID is the technical identity.
- Hard `DrawingRevision + measure_code` uniqueness remains deferred. New UI writes reject an obvious active duplicate at runtime only; the migration deliberately does not encode that profiler-dependent assumption.
- User-facing display rounding remains deferred under ADR-018. Storage accepts exact `numeric(14,5)` inputs without silent rounding.
- Measurement result comparison, boundary outcome and OK/NOK belong to WP-007/WP-008; WP-006 calculates definition limits only.
- `MeasurementGroupArea` rectangle editing and legacy group-area geometry migration are deferred.
- No legacy control-point, group-area or measurement import exists yet; WP-009 owns staging/transform/load/reconciliation.
- No old-version restore/rollback action is included.
