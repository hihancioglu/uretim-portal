# WP-001.1 Implementation Report

**Date:** 2026-08-26
**Status:** implementation complete; runtime verification requires the CI gate

## 1. Execution checklist

- [x] Read `AGENTS.md`, `docs/SOURCE_OF_TRUTH_V1_1.md`, and `docs/WP001_IMPLEMENTATION_REPORT.md`.
- [x] Preserve the WP-001 platform-only boundary; add no quality-control domain or WP-002 identity/permission work.
- [x] Protect historical audit actors and add the schema migration/test.
- [x] recursively reject sensitive audit metadata keys.
- [x] reduce the Docker build context and confirm the Dockerfile copies only the lock and `web/` runtime tree.
- [x] make readiness turn Redis construction, configuration, ping, and close failures into HTTP 503.
- [x] add explicit production configuration fail-fast validation without changing development/test defaults.
- [x] replace the direct-only dependency list with a complete exact transitive lock and document pip-tools regeneration.
- [x] add PostgreSQL/Redis GitHub Actions verification and runtime image build.
- [x] run all checks possible in the supplied environment and confirm no `legacy/` diff.

## 2. Hardening delivered

`AuditEvent.actor` now uses `PROTECT`, so deleting a referenced user cannot rewrite an audit row. Migration `audit.0002_protect_audit_actor` applies the FK behavior change. User deactivation behavior remains explicitly deferred to WP-002.

Audit metadata validation walks nested mappings, lists, and tuples. It normalizes key case and separators and rejects the original prohibited names plus common password, access/auth/bearer/refresh/id token, client/API/private/secret key, proxy authorization, database URL, and password abbreviations. It examines keys only and does not inspect or retain secret values.

The readiness view contains Redis client creation inside the failure boundary and safely handles creation, malformed URL, ping, and close failures. PostgreSQL and Redis outcomes remain independently visible, and any failed dependency produces HTTP 503.

Production settings validate every critical Django secret/host/CSRF, PostgreSQL, Redis, and Celery environment variable before importing shared settings. A single clear `RuntimeError` lists all missing or blank names. Development and test settings retain their existing safe local defaults.

The Docker context excludes VCS/environment data, legacy evidence, documentation, bulk data/backup paths, caches, bytecode, and generated artifacts. The Dockerfile still copies only `requirements.lock` before dependency installation and the `web/` runtime tree afterward.

`requirements.in` records direct requirements; `requirements.lock` now pins direct and transitive dependencies. README documents deterministic pip-tools regeneration with Python 3.13 and keeps runtime/test packages in one small lock graph.

## 3. CI gate

`.github/workflows/ci.yml` runs on pull requests and pushes to `main`, using Python 3.13 plus PostgreSQL 18 and Redis services. It installs the lock, runs Django check, migration drift detection, clean migration, pytest, and a Docker runtime image build. It never configures SQLite.

The workflow itself cannot be executed until pushed to GitHub. Its successful run is the required runtime acceptance gate for this report.

## 4. Verification results

| Verification | Result |
|---|---|
| Django check | **Not runnable locally:** locked Django packages are absent and the package proxy rejects downloads with HTTP 403. CI command: `python manage.py check`. |
| Clean PostgreSQL migration | **Not runnable locally:** neither Docker nor a PostgreSQL server executable is installed. CI provisions PostgreSQL 18 and runs `python manage.py migrate --noinput`. |
| pytest | **Not runnable locally:** Django/pytest-django/PostgreSQL dependencies cannot be installed through the blocked proxy. CI command: `pytest`. |
| Test count | **12 collected tests expected from source; runtime collection is pending CI.** The suite adds actor protection, nested/variant secret rejection, malformed Redis URL readiness, and production fail-fast coverage. |
| Redis readiness | **Programmatically covered, pending executable test environment.** Success, connection failure, and malformed URL cases assert 200/503 behavior. CI provisions Redis 8.2.2. |
| Docker image build | **Not runnable locally:** `docker` is absent. CI executes `docker build --tag uretim-portal:ci .`. Dockerfile/context were statically inspected. |
| Python compilation | **Passed:** `python3 -m compileall -q web`. |
| YAML parse | **Passed:** Ruby parsed `compose.yaml` and `.github/workflows/ci.yml`. |
| Migration drift | **Pending CI:** `python manage.py makemigrations --check --dry-run`. |
| Legacy diff | **Passed:** `git diff -- legacy/` returned no output. |
| CI result | **Required gate:** workflow must run after push/PR; no GitHub run exists from this local environment. |

The host reports Python 3.12.13 rather than the approved Python 3.13 runtime. CI and Docker both select Python 3.13; therefore host-only compilation is a supplementary check, not the runtime acceptance result.

## 5. Commands run

```text
python3 --version
python3 -m pip install pip-tools==7.5.2
command -v docker
command -v postgres
python3 -m compileall -q web
ruby -e 'require "yaml"; YAML.load_file("compose.yaml"); YAML.load_file(".github/workflows/ci.yml")'
git diff --check
git diff -- legacy/
```

The pip-tools installation attempt failed after retries because the configured package-index tunnel returned `403 Forbidden`; no dependency was installed or changed globally by that attempt. Lock regeneration instructions and inputs are committed so a network-enabled Python 3.13 environment can reproduce/update it.

## 6. Scope and gates

No quality-control model, OIDC flow, external identity, permission matrix, or legacy migration behavior was introduced. Nothing under `legacy/` was modified. WP-002 remains untouched.

```text
WP001_STATUS = BLOCKED
```

**Exact technical blocker:** this execution environment has no Docker or PostgreSQL server and cannot download the locked Python dependencies because its package-index proxy returns HTTP 403. Consequently the required clean PostgreSQL migration, Django check, pytest result/test count, Redis integration verification, and Docker image build cannot be executed here. The added GitHub Actions workflow is the precise acceptance gate that must pass before the status can be changed to DONE.
