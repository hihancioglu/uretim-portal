# WP-001 Implementation Report

**Date:** 2026-08-26  
**Status:** implementation complete; runtime verification blocked by this execution environment

## 1. Scope implemented

The WP-001 platform skeleton includes a Django 5.2/PostgreSQL-only project, custom UUID user, generic append-only audit event, Redis/Celery configuration, dependency health endpoints, request correlation and JSON logging, pytest configuration, and Docker Compose services. No quality-control domain model, screen, import, OIDC callback, permission matrix, outbox, or file workflow was implemented.

## 2. Architecture and layout

- `web/config/settings/`: shared, development, test, and production settings.
- `web/apps/accounts/`: the initial custom user model.
- `web/apps/audit/`: generic audit storage and create-only service.
- `web/apps/core/`: health endpoints, correlation context/middleware, and JSON formatter.
- `web/tests/`: platform integration tests.
- `Dockerfile`, `compose.yaml`, and `deploy/nginx.conf`: non-root application image and local PostgreSQL/Redis/web/worker/Nginx topology.

PostgreSQL and Redis have no host-published ports and share an internal backend network. Only Nginx publishes the development HTTP entry point. PostgreSQL data is persistent.

## 3. Dependencies and rationale

Versions are exactly pinned in `requirements.lock`: Django provides the LTS web framework; psycopg is the PostgreSQL driver; Celery and redis provide the worker and Redis client; Gunicorn serves WSGI; pytest and pytest-django provide tests. No frontend framework or unnecessary domain dependency was added.

Dependency resolution could not be repeated in this environment because outbound package index access returned `403 Forbidden`. The application image remains based on Python 3.13 and installs the exact lock entries.

## 4. Migrations

- `accounts.0001_initial`: creates the UUID-primary-key custom user before any project-owned user references.
- `audit.0001_initial`: creates `AuditEvent`, its nullable actor reference, snapshots/context, JSON metadata, and event/time index.

There are no domain migrations. A clean PostgreSQL migration run is specified but could not execute because Docker is unavailable and the host has neither PostgreSQL services nor the locked Python packages.

## 5. Commands executed

```text
python3 --version
docker --version
docker compose version
python3 -m pip index versions Django
python3 -m pip index versions celery
python3 -m pip index versions psycopg
python3 -m pip index versions redis
python3 -m compileall -q web
ruby -e 'require "yaml"; YAML.load_file("compose.yaml")'
git diff --check
git diff -- legacy/
```

The required Docker commands could not be run because the `docker` executable is absent. Package-index commands failed because the environment's network proxy rejected access with HTTP 403.

## 6. Test results

- Python bytecode compilation: passed.
- Compose YAML syntax parse using Ruby's YAML parser: passed.
- Git whitespace check: passed.
- pytest: not runnable; Django, pytest-django, psycopg, Celery, and redis cannot be installed through the blocked package index.
- Django system check and clean PostgreSQL migration: not runnable for the same dependency and Docker limitations.

The committed pytest suite covers the Django system check, custom user setting/UUID key, dependency-free liveness, readiness success and failure, accepted/rejected correlation IDs, audit creation, sensitive metadata rejection, and instance/queryset update/delete prevention.

## 7. Docker Compose validation/run result

`docker compose config`, image build, service startup, clean migration, container pytest, and bounded worker startup are **not verified** because Docker is not installed (`docker: command not found`). The Compose document itself parses as valid YAML. These commands remain mandatory in a Docker-capable review/CI environment:

```bash
cp .env.example .env  # replace placeholders first
docker compose config
docker compose up -d postgres redis
docker compose build
docker compose run --rm web python manage.py check
docker compose run --rm web python manage.py migrate --noinput
docker compose run --rm web pytest
timeout 10s docker compose run --rm worker celery -A config worker --loglevel=INFO
```

## 8. Security notes

- Production secrets and connection configuration are environment-only; `.env` is ignored and `.env.example` contains placeholders.
- Production enables HTTPS redirect, secure/HttpOnly cookies, HSTS, content-type sniff protection, and frame denial. Allowed hosts and CSRF trusted origins are explicit environment inputs.
- Inbound correlation IDs must match the documented conservative ASCII rule and are length-limited; invalid values become UUIDs.
- Structured request logs contain request metadata but no headers, bodies, credentials, or connection strings.
- Audit creation rejects a baseline set of sensitive metadata keys and exposes no update/delete service. Model and queryset mutation paths raise errors. Database-role-level audit immutability is deferred to production deployment design.
- Local password authentication is not exposed as a production flow. OIDC identity linkage is deferred to WP-002. No legacy credentials were read or migrated.

## 9. Files changed

Platform files were added under `web/`, together with root dependency/container configuration, Nginx configuration, `.env.example`, `.gitignore`, `README.md`, and this report. No existing legacy file was changed.

## 10. Legacy confirmation

`git diff -- legacy/` returned no output. The implementation did not modify, rename, delete, format, or generate anything below `legacy/`.

## 11. Deferred items and gates

- **WP-000 gate:** authoritative legacy profiling remains required before WP-003/WP-004 constraints or identities are designed.
- **WP-002 gate:** Authentik/OIDC issuer-subject identity and the approved role/action/scope authorization baseline remain unimplemented.
- Drawing/product identities, storage workflows, domain models, business transitions, notification outbox, production HA/RPO/RTO, TLS automation, database roles, and audit retention remain in their assigned work packages/production gates.
- A Docker-capable environment must run the mandatory verification block above before WP-001 can be declared DONE.

```text
WP001_STATUS = BLOCKED
```

**Exact blocker:** the provided environment has no Docker executable and cannot download the locked Python dependencies (package-index proxy HTTP 403), so clean PostgreSQL migration, pytest, Compose semantic validation/build, and worker startup cannot be evidenced here. The smallest change is to run the listed commands in Docker-capable CI with registry/package access; no business decision is required.
