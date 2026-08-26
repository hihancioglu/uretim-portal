# WP-003 Product and Mold Master Data — Implementation Report

**Date:** 2026-08-26
**Scope:** Product, Mold, and explicit ProductMold master data only

## Execution checklist

- [x] Read `AGENTS.md` and all source-of-truth, domain, mapping, ERD, authorization, foundation-gate, and profiler-runbook inputs named by WP-003.
- [x] Keep WP-000-dependent identity decisions deferred and preserve raw business identifiers.
- [x] Add Product, Mold, and ProductMold models, migration, mutation services, authorization-aware selectors, audit events, and inspection-only Django Admin registrations.
- [x] Add PostgreSQL-oriented tests for identity, cardinality, validation, soft deactivation, permissions, audits, migration policy, and the legacy boundary.
- [x] Run locally available formatting and static checks and verify `legacy/` has no diff.
- [ ] Run Django checks, migration drift, a clean PostgreSQL migration, Django tests, and Docker build (host dependency installation is blocked by the package proxy).
- [ ] Obtain a green CI run for this commit/pull request.

## Schema created

### Product

UUID technical primary key; indexed `tr_code`, `product_name`, and optional `plastic_code`; optional `material` and `color_name`; indexed `is_active`; timezone-aware created/updated timestamps; and nullable, protected created/updated user references.

### Mold

UUID technical primary key; indexed `mold_code`; optional text description; nullable positive integer `cavity_count`; indexed `is_active`; timezone-aware created/updated timestamps; and nullable, protected created/updated user references.

### ProductMold

UUID technical primary key; protected Product and Mold foreign keys; indexed `is_active`; timezone-aware created/updated timestamps; and nullable, protected created/updated user references. The explicit through model supports many products per mold and many molds per product.

## Indexes

- Single-column indexes: Product `tr_code`, `product_name`, `plastic_code`, and `is_active`; Mold `mold_code` and `is_active`; ProductMold `is_active`.
- Composite ProductMold indexes: `(product, is_active)` and `(mold, is_active)` for active relationship traversal.

## Constraints intentionally not created

No uniqueness constraint exists for `Product.tr_code`, `Mold.mold_code`, or `(ProductMold.product, ProductMold.mold)`. Case, whitespace, separators, leading zeros, identity, tokenization, and temporal/cardinality rules remain gated in `WP003_DEFERRED_CONSTRAINTS.md`. UUID primary keys, foreign keys, PostgreSQL field types, and field-boundary cavity validation are the only relevant technical/invariant protections in this package.

The initial migration now preserves the abstract `TrackedModel` related-name templates (`%(app_label)s` and `%(class)s`) exactly as Django's field deconstruction reports them. The earlier hand-written migration expanded those templates prematurely; although the resulting runtime reverse names look the same, that serialized migration state differed from Django 5.2.8's model state and caused six spurious `AlterField` operations. Because WP-003 has not been deployed, correcting `0001_initial.py` is the clean migration-semantic fix and avoids a no-op `0002` migration.

## Service and selector behavior

Explicit transactional mutation services create, update, or deactivate products, molds, and links. Updates lock the target row; normal services never delete master records. Required identifiers are checked for blank-only input without altering accepted values, and cavity count accepts only null or a positive non-boolean integer.

Selectors cover list/get/search operations and ProductMold traversal. Every selector requires `drawings.view`; every mutation requires `drawings.manage` through the existing WP-002 authorization service. No Product-specific permission namespace was added.

## Audit events

Successful business mutations atomically produce:

- `product.created`, `product.updated`, `product.deactivated`
- `mold.created`, `mold.updated`, `mold.deactivated`
- `product_mold.linked`, `product_mold.deactivated`

Events carry actor/entity context. Create/update metadata contains only safe business fields; updates include before/after snapshots. Link metadata contains UUID references only. No signal or model-save audit shortcut was introduced.

## Admin boundary

Product, Mold, and ProductMold are registered in Django Admin for controlled inspection. Add/change/delete are disabled so Admin cannot bypass domain authorization, validation, and transactional audit services.

## Tests and verification

The WP-003 suite covers UUID creation, preserved raw codes, bidirectional many-to-many and repeated-pair capability, duplicate TR/MoldCode acceptance, cavity and blank validation, deactivate-not-delete behavior, anonymous/inactive/unauthorized denial, read authorization, every audit event, update snapshots, absence of uniqueness declarations, and absence of a legacy writer reference.

Local results:

| Check | Result |
|---|---|
| `ruff format --check web/apps/products web/tests/test_wp003.py` | PASS after formatting |
| `ruff check web/apps/products web/tests/test_wp003.py` | PASS |
| `python -m compileall -q web tools tests` | PASS |
| `git diff --check` | PASS |
| `git diff -- legacy/` | PASS — empty |
| `python -m pip install --requirement requirements.lock` | BLOCKED — environment package proxy returned HTTP 403 for `amqp==5.3.1` |
| `python manage.py check` | NOT RUN — Django unavailable after dependency-install failure |
| `python manage.py makemigrations --check --dry-run` | PENDING CI rerun after FIX-001; expected output is `No changes detected` |
| clean PostgreSQL `python manage.py migrate --noinput` | NOT RUN — Django/PostgreSQL tooling unavailable on this host |
| `pytest` from `web/` | NOT RUN — Django/pytest-django unavailable |
| `python -m pytest -p no:django tests/legacy_profiler -q` | PASS — 3 passed |
| `docker build --tag uretim-portal:ci .` | NOT RUN — Docker executable unavailable |
| GitHub Actions | PENDING pull-request run |

No dependency was added and no CI gate was weakened.

## Scope and next gates

No Drawing, DrawingRevision, file/storage, PDF/CAD, control-point, inspection, measurement, SPC/MSA, molding workflow, ticket, test, commissioning, or legacy-import implementation was added. WP-004 has not started.

```text
WP003_STATUS = BLOCKED
EXACT_BLOCKER = GitHub Actions has not yet rerun the complete workflow for WP003-FIX-001.
NEXT_GATE = The existing pull-request CI must pass Django check, migration drift, clean PostgreSQL migrations, Django tests, legacy profiler tests, and Docker build; only then may this report be updated to WP003_STATUS = DONE.
```
