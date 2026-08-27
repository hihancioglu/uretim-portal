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

GitHub CI run #28 verified Django system checks, migration drift (`No changes detected`), clean PostgreSQL migrations, **92 passed** Django tests, **3 passed** legacy-profiler tests, successful official `pdfjs-dist@6.2.108` npm acquisition, collection of 132 static files, both runtime and static-server image builds, and static-server startup/readiness. Its only failure was static asset validation because the standard Nginx 1.29.2 Alpine MIME map does not associate `.mjs` with JavaScript.

FIX-004 patches only the standard MIME table in the static-server image so the existing `application/javascript` entry covers both `js` and `mjs`, then asserts the modified entry during the image build. All other standard CSS, font, image, and asset mappings remain intact. The smoke retains every asset/MIME assertion and now reports each asset path, status, and content type, plus response headers and Nginx logs on failure.

| Check | Result |
|---|---|
| `python -m compileall -q web tools tests` | PASS locally |
| `python manage.py check` | PASS — CI #28 |
| `python manage.py makemigrations --check --dry-run` | PASS — CI #28, `No changes detected` |
| clean PostgreSQL migrations and full pytest | PASS — CI #28, 92 passed |
| legacy profiler pytest | PASS — CI #28, 3 passed |
| official npm acquisition | PASS — CI #28, exact `pdfjs-dist@6.2.108` |
| Docker static collection | PASS — CI #28, 132 files collected |
| runtime and static-server image builds | PASS — CI #28 |
| static-server startup/readiness | PASS — CI #28 |
| production static/PDF.js HTTP and `.mjs` MIME smoke | CI #28 failed at `.mjs` MIME assertion; FIX-004 rerun pending |
| private-volume non-root smoke | Post-fix run pending and retained unchanged |
| `git diff -- legacy/` | PASS locally — empty |
| GitHub CI | CI #28 failed only at `.mjs` MIME validation; FIX-004 rerun pending |

## Known limitations and next gate

`.pdf.enc`, `LEGACY_AES_GCM`, DWG, and DXF remain explicitly unsupported. No decryption, CAD conversion, identity pairing, or control-point persistence was added, and WP-006 was not started. `WP005_STATUS` remains **BLOCKED** at the post-FIX-004 MIME-smoke rerun; completion requires an actual completely green GitHub Actions run including production static delivery and the retained private-volume smoke.
