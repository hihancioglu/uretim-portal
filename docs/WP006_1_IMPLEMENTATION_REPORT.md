# WP-006.1 Implementation Report

**Date:** 2026-08-27  
**Status:** `WP006_1_STATUS = BLOCKED`

## Delivered

Implemented the Turkish server-rendered Technical Drawing Management console covering Product search/create/edit/deactivate, Product Drawing inventory, Drawing create/edit/deactivate, complete revision history, streamed DRAFT upload, DRAFT metadata/file replacement, activation/superseding, withdrawal, protected download/viewer navigation, and return navigation. Every mutation delegates once to an existing audited service and no schema or dependency was added.

Inherited WP-006 cleanup marks its completed CI gates accurately. Copy-source UUID validation now returns safe Turkish JSON for missing, malformed and unknown IDs. Control-point mutation UI now requires both scoped management permission and a DRAFT/ACTIVE revision; historical revisions remain readable.

## Verification status

GitHub Actions run #34 passed the pre-hardening baseline: Django check, migration
drift, clean PostgreSQL migrations, 113 Django tests, 3 legacy-profiler tests,
both Docker images, PDF.js/control-point static smoke and the private-volume
smoke. The final hardening patch adds deterministic pagination, canonical
`.pdf.enc` metadata, pilot lifecycle tests and the `drawings/manage.css` HTTP
smoke. Its new GitHub Actions run is still pending. This report must remain
BLOCKED until that new run is fully green; that uncompleted final verify run is
the exact remaining blocker.

## Gates preserved

No uniqueness assumption, schema migration, authorization role, public file path, storage-key exposure, CAD/decrypt behavior, Mold workflow or WP-007 domain was introduced. `legacy/` remains read-only.
