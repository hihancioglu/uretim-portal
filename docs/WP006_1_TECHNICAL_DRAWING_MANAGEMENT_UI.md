# WP-006.1 Technical Drawing Management UI

## Purpose and execution checklist

- [x] Correct stale WP-006 verification state and harden its copy/read-only UI edges.
- [x] Add an authorized, server-rendered Product → Drawing → Revision console.
- [x] Reuse existing Product, Drawing, file and lifecycle services for every mutation.
- [x] Connect revision rows to the existing protected viewer/control-point authoring UI.
- [ ] Complete all PostgreSQL, pytest, image, smoke and GitHub Actions gates.

The console lets a Technical Drawing or Admin operator search and maintain the Product master, create and maintain Drawings, upload and edit DRAFT revisions, replace DRAFT files, activate/supersede revisions, withdraw revisions, and open the existing viewer without Admin or shell access.

## URL map

The landing page is `/drawings/manage/`. Product create/detail/edit/deactivate routes live below `/drawings/manage/products/`; Drawing create/detail/edit/deactivate routes live below `/drawings/manage/drawings/`; and revision create/edit/replace/activate/withdraw routes live below `/drawings/manage/revisions/`. Existing `/drawings/revisions/<uuid>/view|content|file/` routes are unchanged.

## Authorization and architecture

Every console request requires `drawings.manage`; services independently repeat authorization, and every mutation is a CSRF-protected POST using POST/Redirect/GET. The approved role baseline is unchanged. Views validate Django Forms and orchestrate existing services rather than writing ORM mutations. Product and revision tables use selectors, eager relationships, annotations and 25-row Product pagination.

## Product and Drawing behavior

Product search covers TR code, name and plastic code. Create/edit/deactivate uses Product services and does not infer uniqueness, merge duplicates, cascade deactivation or delete history. Product detail shows all Drawings, including multiple records with the same Product/scope. Drawing create/edit/deactivate similarly preserves revisions and has no Product/scope uniqueness.

## Revision lifecycle and upload

Creation streams `UploadedFile` through `create_drawing_revision_with_file`; replacement uses `replace_draft_revision_file`. DRAFT metadata is limited to revision code/change reason. Activation delegates atomic ACTIVE→SUPERSEDED behavior to the existing service. Withdrawal never selects a replacement. Illegal metadata/file controls are absent and their endpoints reject non-DRAFT state.

Plain PDF is viewable. PDF.ENC, DWG and DXF remain stored/downloadable but unsupported for browser rendering. No decryption, CAD parsing or conversion is introduced.

## Privacy and viewer integration

Templates display only original name, size, MIME-derived capability and shortened SHA-256. They never display storage keys, roots or paths; bytes continue through protected viewer/download endpoints. The existing PDF.js/control-point overlay is reused. Historical SUPERSEDED/WITHDRAWN revisions remain inspectable but their authoring controls are suppressed. The viewer provides a direct management return link for authorized operators.

## Deferred

WP-000 identity/uniqueness decisions, Mold UI/binding, CAD rendering, legacy decryption/import, inspection/measurement/OK-NOK, dashboards and WP-007 remain deliberately out of scope.
