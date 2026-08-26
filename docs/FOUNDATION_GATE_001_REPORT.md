# FOUNDATION-GATE-001 Verification Report

**Date:** 2026-08-26  
**Scope:** WP-000/WP-002 verification and hardening only

## Execution checklist

- [x] Read the required WP-000/WP-002 source-of-truth, reports, authorization baseline, and runbooks.
- [x] Make the repository-root working directory explicit for profiler tests while preserving the `web/` platform checks.
- [x] Move denied OIDC audit creation outside the rollback-prone identity transaction and test every denial reason.
- [x] Freeze the V1 authorization seed data in migration `0004` and add an independence check.
- [x] Run all checks supported by the current host and confirm that `legacy/` is unchanged.
- [ ] Obtain a green GitHub Actions run using Python 3.13, PostgreSQL 18, and Docker.

## Changed files

- `.github/workflows/ci.yml`: explicitly runs profiler tests and the Docker build from `${{ github.workspace }}`; Django checks and tests retain the `web/` job default.
- `web/apps/accounts/identity.py`: performs identity writes in an inner atomic transaction and records a sanitized denial after rollback; invalid issuer/subject is audited without claims.
- `web/apps/accounts/migrations/0004_seed_authorization_baseline.py`: embeds the immutable V1 roles, actions, and grants rather than importing runtime baseline data.
- `web/tests/test_wp002.py`: verifies persistent unknown/inactive/collision/invalid-identity denial events and historical migration independence.
- `docs/FOUNDATION_GATE_001_REPORT.md`: records this gate.

## Verification results

The platform suite contains **32 collected test cases** after parameter expansion (12 platform and 20 WP-002 cases). The profiler suite contains **3 tests**.

| Check | Result |
|---|---|
| `python -m pytest tests/legacy_profiler -q` (repository root) | PASS — 3 passed |
| `python -m compileall web tools tests` | PASS |
| `git diff --check` | PASS |
| `git diff -- legacy/` | PASS — empty |
| `python -m pip install --requirement requirements.lock` | BLOCKED — environment package proxy returned HTTP 403 while resolving `amqp==5.3.1` |
| `python manage.py check` | NOT RUN — Django cannot be installed on this host |
| `python manage.py makemigrations --check --dry-run` | NOT RUN — Django cannot be installed on this host |
| `python manage.py migrate --noinput` against clean PostgreSQL 18 | NOT RUN — PostgreSQL/Django are unavailable on this host |
| `pytest` from `web/` | NOT RUN — Django/pytest-django are unavailable on this host |
| `docker build --tag uretim-portal:ci .` | NOT RUN — Docker is unavailable on this host |
| GitHub Actions | PENDING — requires the committed branch/pull request run |

## OIDC denied-audit persistence

The tests now query `AuditEvent` after `IdentityDenied` escapes for unknown external identity, inactive local user, automatic-provisioning attribute collision, and invalid/missing identity. Only issuer, a SHA-256-derived subject fingerprint, and the fixed denial reason are stored; claims and authentication tokens are not stored. The identity transaction rolls back before the denial audit is created.

## Authorization baseline verification

The existing WP-002 matrix tests plus the new migration check cover issuer+subject uniqueness, default denial of unknown identities, absence of username/email auto-linking, unusable passwords and no automatic business role for provisioned users, inactive user/assignment denial, drawing scopes, Yönetici versus Admin, the MEKANİZMA-only laboratory exception, and idempotent runtime seeding. Migration `0004` now owns an exact V1 snapshot and contains no import of `apps.accounts.baseline`; future baseline changes require a new migration.

No speculative permission, quality-control domain model, or WP-003 work was added.

```text
FOUNDATION_GATE_001 = BLOCKED
FAILING_COMMAND = python -m pip install --requirement requirements.lock
FAILURE = package proxy HTTP 403; no matching distribution available for amqp==5.3.1
PENDING_GATE = GitHub Actions must pass before status may become DONE
```
