# WP-001.2 Implementation Report

**Date:** 2026-08-26  
**Status:** migration drift corrected; a new GitHub Actions run is required before the gate can be declared green

## 1. Execution checklist

- [x] Read `AGENTS.md`, `docs/SOURCE_OF_TRUTH_V1_1.md`, `docs/WP001_IMPLEMENTATION_REPORT.md`, and `docs/WP001_1_IMPLEMENTATION_REPORT.md`.
- [x] Reproduce and inspect the Django 5.2.8 migration drift reported by GitHub Actions.
- [x] Add the normal generated `accounts` migration without changing `accounts.User` or its UUID primary key.
- [x] Confirm the migration contains model/framework state synchronization only.
- [x] Keep the migration-drift CI step unchanged.
- [x] Run all checks available in this environment and confirm no `legacy/` diff.
- [ ] Obtain a green post-fix GitHub Actions run.

## 2. Drift resolution and inspection

The failed CI run identified state drift between `accounts.User`, inherited from Django 5.2.8's `AbstractUser`, and the hand-authored initial migration. The generated migration `accounts.0002_alter_user_options_alter_user_groups_and_more` synchronizes exactly three framework-owned pieces of migration state:

1. inherited singular and plural user display names;
2. the expanded Django help text for the inherited `groups` field; and
3. the expanded Django help text for the inherited `is_active` field.

The migration contains no data migration, custom Python operation, SQL, constraint, index, field type change, default change, authorization rule, OIDC behavior, or quality-control domain behavior. The custom UUID primary key and the `User` model implementation are unchanged. No test change is required because this correction changes descriptive migration state rather than application behavior.

## 3. CI sequence and results

The existing workflow continues to run the required commands in this order:

```text
python manage.py check
python manage.py makemigrations --check --dry-run
python manage.py migrate --noinput
pytest
docker build --tag uretim-portal:ci .
```

The pre-fix GitHub Actions run passed dependency installation and `python manage.py check`, then failed `python manage.py makemigrations --check --dry-run` by proposing the migration now committed. The later migration, pytest, and Docker steps were therefore skipped in that run. This report does not claim those skipped steps passed.

The supplied local environment cannot execute the runtime sequence: it has Python 3.12.13 rather than Python 3.13, has no Docker executable or PostgreSQL server, and dependency installation from `requirements.lock` fails because the configured package proxy returns `403 Forbidden`. Static checks completed successfully:

- `python3 -m compileall -q web`
- `git diff --check`
- `git diff -- legacy/` (no output)

The unchanged `.github/workflows/ci.yml` remains the authoritative Python 3.13/PostgreSQL 18/Redis runtime gate. A new GitHub Actions run must confirm `No changes detected`, clean migration, pytest, and the Docker build before WP-001 is DONE.

## 4. Scope confirmation

Nothing under `legacy/` was changed. WP-002 was not started. No OIDC, Authentik, role, permission, product, drawing, inspection, or measurement model or behavior was added.

```text
WP001_STATUS = BLOCKED
```

**Exact current blocker:** the post-fix GitHub Actions CI gate has not yet run green. Local execution of `python manage.py check` (and the following Django commands) is blocked at import time with `ModuleNotFoundError: No module named 'django'`; installing `requirements.lock` fails with `ERROR: Could not find a version that satisfies the requirement amqp==5.3.1` after the package proxy returns `403 Forbidden`, and `docker build --tag uretim-portal:ci .` cannot run because `docker: command not found`.
