# WP-000 Implementation Report

**Date:** 2026-08-26  
**Implementation status:** DONE  
**Authoritative data gate:** BLOCKED — no live factory data was available in this environment.

## Execution checklist

- [x] Read the prescribed architecture, domain, mapping, ERD, master-plan, gap-analysis, and WP-001 reports in order.
- [x] Inspect only relevant read-only legacy evidence (`AppPaths`, `DataService`, `ProductInfo`, and `CryptoService`).
- [x] Implement a standalone standard-library, read-only profiler and deterministic CLI.
- [x] Add synthetic security, encoding, delimiter, malformed-row, identity, numeric, datetime, drawing, immutability, hash, and determinism tests.
- [x] Add runtime-output ignore rule and an IT runbook.
- [x] Run available checks, preserve the existing CI gate, and verify no `legacy/` diff.

## Implementation and files

`tools/legacy_profiler/` contains CLI orchestration, discovery/hash collection, CSV decoding/profiling, central redaction, identity lenses, drawing classification, and deterministic report writers. `tests/legacy_profiler/test_profiler.py` creates synthetic UTF-8-BOM/cp1254 Turkish sources and drawing files at test runtime. `.gitignore` excludes the recommended runtime artifact location. This report and `WP000_LEGACY_PROFILER_RUNBOOK.md` document execution and gates.

Artifacts include summary/CSV/numeric/datetime/drawing/warning JSON, manifest/duplicate/reference CSV, and a structural Markdown summary. Sensitive columns expose only presence and blank counts. Personal columns are not sampled. No hash or sample of a sensitive value is emitted.

## Legacy evidence and expected catalog

- `legacy/TeknikResimOlcum/Services/AppPaths.vb`, class `AppPaths`: `Data`, `Drawings`, key-file separation, and named CSV locations.
- `legacy/TeknikResimOlcum/Services/DataService.vb`, header arrays and CSV operations: Product, control-point, measurement/`RecordId`, drawing, mold, actor, numeric, and datetime column evidence.
- `legacy/TeknikResimOlcum/Models/ProductInfo.vb`, `NormalizeDrawingScope`/`DrawingScopeFolder`: drawing scopes and folder evidence only; not copied as final identity.
- `legacy/TeknikResimOlcum/Services/CryptoService.vb`, `EncryptedDrawingFormat`/`InspectEncryptedDrawingFormats`: `.enc` and safe format-header evidence. The profiler reads only a small header and never loads a key or decrypts.

The embedded expected core catalog covers Users, Products, ControlPoints, MeasurementGroupAreas, MeasurementRecords/Corrections, SPC corrections, visual/closed-eye records, audit, production/binding/mold/quality tickets, connection plans, commissioning, test records/catalog/groups, devices, and INO. Discovery also inventories every supplied file recursively, so optional sources in `LEGACY_MAPPING_V1.md` appear when present. Missing expected sources are evidence, not fatal errors.

## Dependencies, scope limitations, and unresolved gates

No dependency was added; implementation uses Python's standard library and does not change either requirements file. No Django app, ORM model, migration, database write, source repair, decrypt path, inferred timestamp relationship, business-key decision, or uniqueness constraint was introduced. Optional SQL files receive file metadata only and remain explicitly non-authoritative. Reference checking currently implements the mapping-supported Product `DrawingFile` to drawing-estate filename check; other unclear FK semantics remain unimplemented rather than guessed.

The V2 category is detected only through the non-secret `TRDRAW2` header evidenced by legacy code; other encrypted files are reported as `V1_or_unknown`. Datetime parsing is an experiment over explicit formats; naive values are labeled with `Europe/Istanbul` as a candidate policy and are never converted. Numeric experiments use `Decimal` and do not choose rounding semantics.

Authoritative profiling was **not performed**. IT's next exact step is:

```bash
python -m tools.legacy_profiler --root /mnt/legacy-data-ro --output /var/secure/legacy-profile/current --profile-name authoritative-2026-08-26
```

Then follow the in-company review and sanitization procedure in the runbook.

## Commands and results

- `python3 -m pytest -q tests/legacy_profiler`: **passed, 3 tests**.
- `python3 -m compileall -q tools tests/legacy_profiler`: **passed**.
- `git diff --check`: **passed**.
- `git diff -- legacy/`: **passed, empty output**.
- `pytest -q` from `web/`: **environment-blocked during collection** because this host lacks Django/pytest-django (`ModuleNotFoundError: django`). The unchanged CI runtime installs `requirements.lock` on Python 3.13 and runs the platform suite.
- Host runtime: Python 3.12.13; the approved Python 3.13 runtime remains selected by CI/container configuration.

Clean PostgreSQL migration, Django system/migration-drift checks, and Docker build were not rerun locally because this environment lacks the locked Django stack and Docker/PostgreSQL runtime, as documented by WP-001. The existing CI checks were not weakened; a separate synthetic profiler test step was added.

```text
WP000_IMPLEMENTATION_STATUS = DONE
WP000_DATA_GATE_STATUS = BLOCKED
```
