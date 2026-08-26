# WP-005 Secure PDF.js Viewer Architecture

## Execution checklist

- [x] Preserve WP-004 identity, lifecycle, download, authorization, and private-storage boundaries.
- [x] Add independently authorized revision HTML and PDF content routes.
- [x] Add bounded single-range streaming through the storage adapter.
- [x] Add a current-page, high-DPI viewer and an empty normalized overlay plane.
- [x] Add collectstatic-backed Nginx delivery without mounting private drawings.
- [ ] Vendor the pinned upstream PDF.js distribution (blocked by the execution environment's HTTP 403 policy).
- [ ] Run PostgreSQL, Docker, and GitHub CI gates.

## Request and authorization flow

```text
Browser
  |
  +-- /static/... local PDF.js/application assets -> Nginx /srv/static
  |
  +-- /drawings/revisions/<uuid>/view/
  |        +-- drawings.view + DRAWING scope matching Drawing.scope
  |
  +-- /drawings/revisions/<uuid>/content/
           +-- independent drawings.view + DRAWING scope check
           +-- FileObject capability metadata
           +-- private storage adapter
                    +-- /data/drawings (application only; never public)
```

Both revision-centric routes address a `DrawingRevision` only by UUID and use the existing `require_scoped_action` implementation. Authorization occurs before any response metadata is rendered. All revisions listed on the page belong to the already-authorized Drawing, so duplicate revision codes remain valid navigation entries. The existing attachment route remains separate.

## Capability matrix

| File | Encryption | Viewer |
|---|---|---|
| case-insensitive `.pdf` | `NONE` | Supported |
| `.pdf.enc` | any | Unsupported (415 at content route) |
| `.pdf` | `LEGACY_AES_GCM` | Unsupported (415); no key/decryption |
| `.dwg`, `.dxf`, any other suffix | any | Unsupported (415) |

The suffix and encryption scheme are both checked server-side; client MIME metadata is never sufficient. Missing physical content is a safe 404.

## Private byte endpoint and ranges

A no-Range GET returns 200 and a bounded streaming iterator. A valid `bytes=start-end`, `bytes=start-`, or `bytes=-suffix` request returns the exact interval with 206. Only one range is accepted. Malformed, multiple, reversed, zero-suffix, and start-past-EOF ranges return 416 and `Content-Range: bytes */size`. HEAD returns the same representation metadata without a body. Responses use `application/pdf`, `Accept-Ranges: bytes`, exact `Content-Length`, `private, no-store`, and `nosniff`. Inline filenames remove CR/LF and both path-separator forms.

The view only passes an opaque storage key to `apps.core.storage`; it cannot construct or observe a filesystem path. `size` and `iter_range` form a future object-storage migration seam. Storage roots, keys, and Docker paths are never browser data.

## PDF.js and local asset strategy

The selected pin is official Mozilla PDF.js **2.14.305** (Apache-2.0), matching the distribution packaged from Mozilla's `pdf.js` source. Required files are `pdf.min.js`, `pdf.worker.min.js`, upstream `LICENSE`, and provenance metadata under `web/apps/drawings/static/vendor/pdfjs/2.14.305/`. Runtime CDN use is forbidden.

**Current gate:** those four vendored upstream files could not be retrieved because every allowed registry/archive request in this environment returned HTTP 403. They have intentionally not been replaced with unofficial, hand-written, or CDN-loaded substitutes. The architecture is therefore implemented but WP-005 remains blocked until reviewed upstream bytes can be committed and their SHA-256 values recorded.

## Production static delivery

The Docker `static-collector` stage runs Django `collectstatic`; `static-server` copies only that output to `/srv/static`; Nginx serves `/static/` locally. Nginx receives neither `drawing_data` nor `/data/drawings`. The runtime target remains the non-root application image. Compose builds the Nginx target, and CI starts it and checks the application stylesheet plus both PDF.js assets for successful, non-empty HTTP responses.

## Viewer and WP-006 overlay contract

The viewer keeps one current PDF page in memory, cancels stale render tasks, bounds zoom to 25–500%, supports previous/next/direct page navigation, 100%, fit-page and fit-width modes, and responds to viewport resizing. Device pixel ratio (capped at 3 for allocation safety) controls only canvas backing resolution. Canvas CSS size, page-stage size, and empty overlay size are identical.

After a successful render, `drawingviewer:rendered` contains only `pageNumber`, `cssWidth`, and `cssHeight`. WP-006 can later interpret overlay positions as `page_no`, `x_ratio`, and `y_ratio` independent of DPI, zoom, and bitmap dimensions. WP-005 adds no control-point model, API, or persistence.
