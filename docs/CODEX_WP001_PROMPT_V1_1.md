# Codex Task Prompt — WP-001 Platform Skeleton v1.1

You are implementing **WP-001 only** in the A Blok Kalite Kontrol web migration repository.

## Read first

Before changing code, read:

1. `AGENTS.md`
2. `docs/SOURCE_OF_TRUTH_V1_1.md`
3. `docs/DOMAIN_RULES_V1.md`
4. `docs/POSTGRESQL_ERD_V1.md`
5. `docs/LEGACY_MAPPING_V1.md`
6. `docs/WEB_DONUSUM_MASTER_PLANI_V1.md`
7. `docs/CODEX_ANALYSIS_V1.md`

Treat `legacy/` as **read-only AS-IS evidence**.

`SOURCE_OF_TRUTH_V1_1.md` overrides older documents where they conflict.

---

## Goal

Create a production-oriented but minimal Django/PostgreSQL platform skeleton that is safe to build future work packages on.

This task must **not** port quality-control domain screens or data models.

---

## Approved technical baseline

- Python 3.13.x
- Django 5.2 LTS series; resolve and pin the current compatible 5.2.x patch in the dependency lock
- PostgreSQL 18.x
- Redis + Celery
- Django Templates + HTMX + Alpine.js baseline; no React/Vue
- pytest + pytest-django
- Docker Compose
- `USE_TZ=True`
- `TIME_ZONE="Europe/Istanbul"`
- Production authentication direction is Authentik/OIDC, but full OIDC is WP-002

Do not use SQLite, including as a test fallback.

---

## Exact scope

### 1. Repository/project structure

Create the new web application under `web/`.

Use a clear layout similar to:

```text
web/
  manage.py
  config/
  apps/
    accounts/
    audit/
    core/
  templates/
  static/
  tests/
```

Do **not** create all future domain apps in this WP. They will be created by their own work packages.

### 2. Settings

Implement environment-separated settings, for example:

- base/common
- development
- test
- production

Requirements:

- environment variables for secrets/config
- PostgreSQL only
- timezone-aware settings
- secure production defaults where practical
- no secret values committed
- explicit `ALLOWED_HOSTS`, CSRF trusted origins and secure-cookie configuration via environment

### 3. Custom user model — mandatory before first migration

Create the minimal custom user model now and set `AUTH_USER_MODEL` before migrations are generated.

Requirements:

- UUID primary key
- compatible with future Authentik/OIDC mapping
- no legacy credential fields
- do not migrate or model legacy password/hash/salt fields
- full external OIDC identity model is **not** part of WP-001
- production local-password authentication flow is **not** implemented in this WP

Do not over-design role/scope permissions here; that is WP-002.

### 4. Minimal audit platform model

Create a minimal append-only `AuditEvent` model suitable for future domain events.

It may include fields such as:

- UUID id
- occurred_at
- actor FK nullable
- actor snapshot
- event type
- entity type/id snapshot reference
- correlation/request id
- IP/user-agent where available
- metadata JSON for non-secret structured context

Requirements:

- no business-domain transition model yet
- no passwords/tokens/secrets in audit metadata
- normal application services must not expose update/delete operations for audit rows

### 5. PostgreSQL

Configure PostgreSQL as the only database backend.

Do not create:

- product/mold
- drawing/revision
- control point
- measurement/inspection
- tickets/binding
- SPC/MSA/lab/package/commissioning
- legacy staging/import tables

Those are outside WP-001.

### 6. Redis/Celery

Add a minimal Celery application and worker configuration.

Requirements:

- worker starts successfully
- Redis broker/backend configuration via environment
- no domain tasks yet
- optional simple smoke/debug task is acceptable only if it remains clearly platform-only

### 7. Health endpoints

Implement:

- `GET /health/live` — process liveness, must not depend on PostgreSQL/Redis
- `GET /health/ready` — verify at least PostgreSQL and Redis connectivity

Return small machine-readable responses and correct non-2xx readiness when a required dependency is unavailable.

Do not make Celery worker availability a synchronous web readiness dependency.

### 8. Request correlation and structured logging

Implement middleware/utilities so each request has a correlation/request id.

Requirements:

- accept a valid inbound correlation id only under a documented safe rule, otherwise generate one
- include it in response headers
- include it in structured application logs
- make it available for future audit calls
- do not log secrets

### 9. Docker Compose

Provide local/integration services:

- web
- worker
- postgres
- redis
- nginx may be included as a simple reverse-proxy skeleton; do not solve production TLS/certificate automation in this WP

Requirements:

- persistent PostgreSQL volume
- healthchecks where useful
- internal networking; PostgreSQL/Redis should not be unnecessarily exposed externally
- non-root application container where practical
- `.env.example` with placeholders only

### 10. Testing

Configure pytest/pytest-django.

Tests must use PostgreSQL, not SQLite.

At minimum test:

- Django boot/system check
- custom user model is configured
- migration succeeds on clean database
- liveness endpoint
- readiness endpoint success path
- readiness failure behavior where practical
- request correlation header/middleware
- audit event creation and absence of normal update/delete service API

### 11. Documentation

Create/update:

- project README/local development commands
- `docs/WP001_IMPLEMENTATION_REPORT.md`

The implementation report must contain:

1. scope implemented
2. architecture/layout created
3. dependencies added and why
4. migrations created
5. commands executed
6. test results
7. Docker Compose validation/run result
8. security notes
9. files changed
10. explicit confirmation that `legacy/` was not modified
11. deferred items/gates for WP-000 and WP-002

---

## Explicit non-goals / forbidden work

Do not:

- modify anything under `legacy/`
- port a WinForms form
- copy `Sql/Schema.sql`
- add a CSV runtime provider
- implement legacy CSV import
- create drawing/product/control-point models
- implement Authentik/OIDC callbacks yet
- create production role/permission scope matrix yet
- implement email notification/outbox domain workflows
- implement file upload/download or drawing decrypt
- implement PDF.js/CAD
- add React/Vue
- use SQLite
- use Django signals for future business workflow
- put future business rules in views or model `save()` overrides
- add speculative DB constraints for unresolved ADRs

---

## Implementation discipline

Before coding, provide a concise checklist of the changes you intend to make.

Then implement WP-001 completely.

If a choice is not required to complete WP-001 and is still an open ADR, do not decide it. Record it in the implementation report as deferred.

Do not stop merely because `CODEX_ANALYSIS_V1.md` previously says `READY_FOR_WP001 = NO`; `SOURCE_OF_TRUTH_V1_1.md` resolves that by changing the scope.

---

## Required verification commands

Use the repository's actual command names/files, but verify the equivalent of:

```bash
docker compose config

docker compose up -d postgres redis

# build/start application services as defined by the project
docker compose build
docker compose run --rm web python manage.py check
docker compose run --rm web python manage.py migrate --noinput
docker compose run --rm web pytest

# worker must be able to start/import app; use a bounded check rather than leaving it attached forever

git diff -- legacy/
```

If Docker is unavailable in the execution environment, do not fake success. Run all possible non-Docker checks and document the exact unavailable verification in `WP001_IMPLEMENTATION_REPORT.md`.

---

## Definition of Done

WP-001 is DONE only when:

- [ ] custom `AUTH_USER_MODEL` exists before first project migration
- [ ] Django runs against PostgreSQL
- [ ] no SQLite runtime/test fallback exists
- [ ] migrations succeed on a clean PostgreSQL DB
- [ ] Redis connectivity is configured
- [ ] Celery worker can import/start
- [ ] `/health/live` passes
- [ ] `/health/ready` validates PostgreSQL + Redis
- [ ] correlation id is returned and logged
- [ ] minimal append-only audit model works
- [ ] pytest passes
- [ ] Docker Compose config is valid
- [ ] secrets are not committed
- [ ] no quality-domain model/screen was implemented
- [ ] `git diff -- legacy/` is empty
- [ ] `docs/WP001_IMPLEMENTATION_REPORT.md` exists and is accurate

At the end, report:

```text
WP001_STATUS = DONE
```

or

```text
WP001_STATUS = BLOCKED
```

If blocked, list the exact blocker, evidence, and the smallest required decision/change. Do not begin WP-002.
