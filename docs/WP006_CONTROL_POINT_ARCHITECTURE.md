# WP-006 Control Point Architecture

## Execution checklist

- [x] Keep legacy evidence read-only and implement only control-point definition authoring.
- [x] Add logical/version models, structural constraints, transactional services, selectors and audit.
- [x] Extend the WP-005 CSS overlay contract without changing PDF.js or private content delivery.
- [x] Add scoped server authorization, editor/detail/history UI, copy and deactivation flows.
- [x] Add PostgreSQL-oriented tests, migration and deferred-decision record.
- [ ] Verify clean PostgreSQL migration, complete pytest, images and GitHub Actions in CI.

## Aggregate and lifecycle

`ControlPoint` is the stable logical measurement owned by one `Drawing`; its UUID and `spc_key` survive definition changes. A web-created point initially uses `measure_code` for both `spc_key` and `logical_code`. Ordinary revision never changes `spc_key`.

`ControlPointVersion` is an immutable-in-meaning definition snapshot. A revise transaction locks the revision, logical point and current version, closes the current version, selects the next point-wide `version_no`, and inserts a new active row. Deactivation closes only the active row for that drawing revision. Historical rows are protected rather than deleted. The database allows at most one active row per `(control_point, drawing_revision)` while allowing active definitions of that logical point on different drawing revisions.

DRAFT and ACTIVE drawing revisions accept mutation. SUPERSEDED and WITHDRAWN remain viewable but read-only. Services verify the logical point and drawing revision have the same Drawing UUID.

## Decimal definition contract

Factory strings accept one optional sign and either one decimal comma or point. Values with ambiguous syntax, more than five fractional digits, non-finite values, or values outside `numeric(14,5)` are rejected; no locale parser, float, or silent quantization is used. The server canonicalizes `lower_tolerance=-abs(input)`, `upper_tolerance=abs(input)`, then calculates `lower_limit=nominal-abs(lower)` and `upper_limit=nominal+abs(upper)`. The browser never supplies limits.

## Overlay geometry and HTTP UI

WP-005 remains responsible for PDF.js 6.2.108, private Range delivery, rendering, zoom and canvas allocation. WP-006 listens to `drawingviewer:rendered`, filters the authorized endpoint by `pageNumber`, and renders each marker at `left=x_ratio*100%`, `top=y_ratio*100%`. Placement divides CSS pointer offsets by `overlay.getBoundingClientRect()` dimensions, clamps them to `[0,1]`, and submits six-decimal strings. Canvas backing pixels, zoom and device pixel ratio never enter persisted geometry.

The server-rendered page conditionally exposes management controls, but every list/detail/create/revise/deactivate/copy request independently applies WP-002 `drawings.view` or `drawings.manage` with `scope_type=DRAWING` and `scope_key=Drawing.scope`. JSON contains definition data only—not storage keys or paths. Click placement remains transient until a valid CSRF-protected form submission succeeds. Marker details and read-only history are available to viewers; revising/repositioning creates one deliberate new version.

## Explicit copy and WP-007 selector

`copy_control_points_to_revision(actor, source_revision, target_revision)` locks both same-Drawing revisions and relevant points, requires `drawings.manage`, requires a DRAFT empty target, and copies source-active definitions to new version rows that reuse logical IDs and stable SPC keys. It is atomic and never runs implicitly.

WP-007 must use `list_active_versions_for_revision(actor, drawing_revision)` (or its page variant). It enforces `drawings.view`, returns only active definitions, eagerly loads safe relationships, and orders by `sort_no`, `measure_code`, then UUID. WP-007 must not reproduce the ORM applicability rule ad hoc.
