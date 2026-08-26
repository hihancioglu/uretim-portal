# WP-004 Drawing, Revision and Private Storage — Implementation Report

**Date:** 2026-08-26

## Execution checklist

- [x] Read the required source-of-truth, domain, mapping, ERD, authorization, WP-003 and profiler documents.
- [x] Implement only Drawing/FileObject/revision/storage behavior and retain WP-000 identity gates.
- [x] Add explicit authorized services, audit events, private streaming access and inspection-only Admin.
- [x] Add migrations, PostgreSQL-oriented tests, configuration and operational documentation.
- [x] Verify formatting/static checks and the legacy boundary.
- [ ] Obtain green CI results for the FIX-002 authentication-isolation correction and private-volume smoke gate.

## Schema, indexes, and constraints

`core_file_object` stores a UUID, filesystem backend, unique opaque technical storage key, display name/MIME, positive size, indexed non-unique lowercase SHA-256 metadata, NONE/LEGACY_AES_GCM metadata, and protected creator/timestamp. Bytes and absolute paths are absent.

Drawing stores a UUID, protected Product, canonical scope code, optional title, active flag and actor timestamps. DrawingRevision stores a UUID, protected Drawing/FileObject, unnormalized required revision code, lifecycle/effective/approval data and actor timestamps. A PostgreSQL conditional unique constraint allows at most one `ACTIVE` revision per Drawing UUID. No Product+scope, revision-code, or filename identity constraint exists; see `WP004_DEFERRED_IDENTITY.md`.

## Services, authorization, audit, and storage

Explicit transactional services create/update/deactivate drawings, create/update/replace draft revisions, activate and withdraw. Activation locks the Drawing and revisions, supersedes the current active revision, timestamps both sides, and relies on the database invariant as final protection. Normal services cannot edit active or historical revisions and never delete historical records/files.

Mutations require `drawings.manage`. The streaming endpoint requires the existing `drawings.view` action plus a `DRAWING` scope grant matching the stable Drawing scope. It provides private/no-store and nosniff headers and a sanitized attachment name without exposing storage paths. All revision statuses, including withdrawn historical records, remain downloadable to an authorized scoped reader; withdrawal removes workflow currency, not audit visibility. Missing physical objects return 404.

Filesystem keys use `objects/<2>/<2>/<uuidhex>`, never names/business identifiers. Writes stream through a temporary file, calculate SHA-256/size, enforce extension/zero/size policy, fsync, and atomically finalize. The same private Compose volume is mounted to web/worker and never Nginx.

The image bootstraps `/data/drawings` as mode `0700`, owned by the non-root `app:app` runtime identity, before `USER app`. Fresh named volumes inherit that directory metadata. CI now verifies the built image can write/read/remove an object through a fresh named volume while its effective UID remains non-root.

Draft file replacement now uploads before acquiring the database lock, then creates FileObject metadata, changes the revision, and writes both audits in one transaction after a locked final DRAFT assertion. Any validation/database/audit failure rolls back all metadata/audits and compensates the new physical object. Service-created encryption metadata accepts only `NONE` and `LEGACY_AES_GCM`; no scheme is inferred and no cryptography is implemented.

Events implemented: `drawing.created`, `drawing.updated`, `drawing.deactivated`, `file_object.created`, `drawing_revision.created`, `drawing_revision.updated_draft`, `drawing_revision.file_replaced`, `drawing_revision.activated`, `drawing_revision.superseded`, and `drawing_revision.withdrawn`. Events contain safe IDs/state/hash/size values and never roots, absolute paths, keys, secrets, or bytes.

## Verification result

- `ruff format ...`: PASS.
- `ruff check ...`: PASS.
- GitHub CI #22 reached the full pytest suite: 69 collected, 67 passed, and 2 endpoint tests failed because `Client.force_login()` implicitly instantiated the production-only OIDC backend without its token endpoint setting. The endpoint tests now override authentication to Django's `ModelBackend` only within those tests and pass that backend explicitly to every `force_login()` call; production OIDC settings and behavior are unchanged.
- Django migration drift/migrate/full pytest: BLOCKED locally because Django dependencies are unavailable and the package index is inaccessible (`pip install -r requirements.lock` received HTTP tunnel 403). The FIX-002 CI result is pending.
- Docker build and fresh-volume non-root write/read/remove smoke gate: not executed by CI #22 because pytest failed first, and BLOCKED locally because Docker is unavailable. The FIX-002 CI result is pending.
- `git diff -- legacy/`: PASS, empty.

No PDF.js/rendering, crypto implementation, CAD parsing, control point, inspection, measurement, SPC/MSA, ticket, commissioning, or legacy import was introduced.

```text
WP004_STATUS = BLOCKED
```
