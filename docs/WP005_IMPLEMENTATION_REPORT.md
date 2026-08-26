# WP-005 Secure PDF.js Viewer — Implementation Report

**Date:** 2026-08-26  
**Status:** `WP005_STATUS = BLOCKED`

## Summary

Implemented revision-centric viewer/content routing, real WP-002 Drawing-scope authorization, safe capability messages, bounded full/range/HEAD streaming through the storage abstraction, responsive current-page viewer structure, high-DPI canvas sizing, empty WP-006 overlay contract, production collectstatic/Nginx image delivery, static smoke CI, and server/template tests. No database model or identity rule changed; WP-006 was not started.

Routes added:

- `GET /drawings/revisions/<uuid>/view/`
- `GET|HEAD /drawings/revisions/<uuid>/content/`

The content route supports plaintext `.pdf` with encryption `NONE`. `.pdf.enc`, `LEGACY_AES_GCM`, DWG, DXF, and all other types remain unsupported; no decryption, pairing, CAD conversion, or key was introduced. Single closed/open/suffix ranges return 206, invalid or unsatisfiable ranges return 416, and all successful content is private/no-store and nosniff.

## Files changed

- Storage abstraction: bounded size/range reads.
- Drawing views/URLs, server template, viewer module, and styling.
- Multi-stage Docker static collection, Nginx static route, Compose static-server target.
- CI production-static HTTP smoke gate while retaining the private-volume gate.
- WP-005 tests, this report, architecture document, and storage runbook.

## Test coverage added

Authorization cases cover anonymous, no-role, wrong Drawing scope, correct scope, and broad `manager` through the real authorization data model. Tests also cover UUID navigation with duplicate revision codes, safe metadata, unsupported types, full and three range forms, invalid ranges, HEAD, missing objects, safe filename disposition, local-only asset references, overlay structure, and vendored-file presence. The WP-004 download route and its existing authorization tests are unchanged.

## Blocking gate and verification record

The exact PDF.js pin selected is **2.14.305**. Required official distribution bytes could not be downloaded: `apt-get download pdf.js-common` and the npm registry request both failed with `403 Forbidden` from the environment proxy. No unofficial replacement or runtime CDN was used. Consequently the vendored-asset existence test and Docker static smoke cannot pass, plaintext PDF rendering is unavailable, and acceptance correctly remains blocked.

| Check | Result |
|---|---|
| `python -m compileall -q web tools tests` | PASS |
| `python manage.py check` | Blocked locally: dependencies unavailable |
| `python manage.py makemigrations --check --dry-run` | Blocked locally: dependencies unavailable; no model/migration touched |
| clean PostgreSQL migration / full pytest / profiler pytest | Pending final run |
| Docker runtime/static builds and smoke gates | Blocked until official PDF.js files are present; Docker availability pending |
| `git diff -- legacy/` | PASS — empty |
| GitHub CI | Pending; cannot be green while the upstream asset gate is open |

## Known limitations and next gate

A reviewer/operator with approved upstream access must add the unmodified official `pdf.min.js`, `pdf.worker.min.js`, and LICENSE for 2.14.305, record URL/checksums in `PROVENANCE.md`, then run every required PostgreSQL, migration, profiler, Docker, static HTTP, private-volume, and GitHub CI gate. Do not mark this WP done before all are green. `.pdf.enc`, DWG, and DXF remain explicitly unsupported. WP-006 control points were not started.
