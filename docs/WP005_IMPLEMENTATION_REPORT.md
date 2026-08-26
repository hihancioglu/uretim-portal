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

Authorization cases cover anonymous, no-role, wrong Drawing scope, correct scope, and broad `manager` through the real authorization data model. Tests also cover UUID navigation with duplicate revision codes, safe metadata, unsupported types, full and three range forms, invalid ranges, HEAD, missing objects, safe filename disposition, local-only asset references, overlay structure, and Docker-build acquisition configuration and local ESM security settings. The WP-004 download route and its existing authorization tests are unchanged.

## PDF.js security correction and verification record

The earlier vulnerable/blocked pin was abandoned. PDF.js is now pinned to **6.2.108** from the official `pdfjs-dist@6.2.108` npm package. An isolated Node build stage acquires it at Docker build time and copies only `pdf.mjs`, `pdf.worker.mjs`, and `LICENSE` into collectstatic input. Node/npm is absent from the runtime and Nginx images. Browser assets remain local and same-origin, with PDF scripting and eval support explicitly disabled.

GitHub CI run #26 verified Django system checks, migration drift (`No changes detected`), clean PostgreSQL migrations, **91 passed** Django tests, **3 passed** legacy-profiler tests, and successful official `pdfjs-dist@6.2.108` npm acquisition. It then failed only in `static-collector`, because that stage incorrectly invoked fail-fast production settings without the complete runtime environment.

FIX-002 adds a dedicated build-only settings module based directly on base settings and switches only `collectstatic` to it. It supplies the minimum harmless secret/password strings needed to import base configuration and does not require or contact PostgreSQL or Redis. Production settings and `REQUIRED_ENVIRONMENT` are unchanged; the existing fail-fast test remains, and a new subprocess test proves build settings load without production database, Redis, CSRF, drawing-root, or host variables.

| Check | Result |
|---|---|
| `python -m compileall -q web tools tests` | PASS locally |
| `python manage.py check` | PASS — CI #26 |
| `python manage.py makemigrations --check --dry-run` | PASS — CI #26, `No changes detected` |
| clean PostgreSQL migrations and full pytest | PASS — CI #26, 91 passed |
| legacy profiler pytest | PASS — CI #26, 3 passed |
| official npm acquisition | PASS — CI #26, exact `pdfjs-dist@6.2.108` |
| Docker static collection | CI #26 failed at production-settings environment validation; FIX-002 post-change rerun pending |
| production static/PDF.js HTTP smoke | Not reached in CI #26; pending successful static collection |
| private-volume non-root smoke | Post-fix run pending and retained unchanged |
| `git diff -- legacy/` | PASS locally — empty |
| GitHub CI | CI #26 failed at static collection; FIX-002 rerun pending |

## Known limitations and next gate

`.pdf.enc`, `LEGACY_AES_GCM`, DWG, and DXF remain explicitly unsupported. No decryption, CAD conversion, identity pairing, or control-point persistence was added, and WP-006 was not started. `WP005_STATUS` remains **BLOCKED** at the post-FIX-002 static-collection rerun; completion requires an actual completely green GitHub Actions run including production static delivery and the retained private-volume smoke.
