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

GitHub CI run #25 reached the full Django suite: **89 collected, 88 passed, 1 failed**. Its sole failure was the obsolete source-tree vendored-file assertion. FIX-001 replaces that assertion with deterministic ESM/version/scripting/no-CDN tests and moves real asset existence/delivery verification to the Docker static-server smoke gate. A post-fix GitHub run has not yet completed, so this report correctly remains BLOCKED rather than claiming expected results as actual results.

| Check | Result |
|---|---|
| `python -m compileall -q web tools tests` | PASS locally |
| `python manage.py check` | BLOCKED locally: Django dependency unavailable; post-fix CI pending |
| `python manage.py makemigrations --check --dry-run` | BLOCKED locally: Django dependency unavailable; no model or migration changed |
| full PostgreSQL pytest | CI #25: 88 passed, 1 obsolete asset-test failure; post-fix run pending |
| legacy profiler pytest | CI #25 reached pytest successfully; post-fix result pending |
| Docker runtime/static builds and static HTTP smoke | Post-fix run pending; must acquire official npm package successfully |
| private-volume non-root smoke | Post-fix run pending and retained unchanged |
| `git diff -- legacy/` | PASS locally — empty |
| GitHub CI | Post-fix result pending; WP-005 remains BLOCKED until completely green |

## Known limitations and next gate

`.pdf.enc`, `LEGACY_AES_GCM`, DWG, and DXF remain explicitly unsupported. No decryption, CAD conversion, identity pairing, or control-point persistence was added, and WP-006 was not started. The only completion gate is an actual completely green post-fix GitHub Actions run, including npm acquisition, production static delivery, and the retained private-volume smoke.
