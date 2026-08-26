# AGENTS.md — A Blok Kalite Kontrol Web Migration

This repository is a controlled rewrite of the legacy VB.NET WinForms quality-control application into Django/PostgreSQL.

## 1. Instruction priority

Read and obey, in this order:

1. `AGENTS.md`
2. `docs/SOURCE_OF_TRUTH_V1_1.md`
3. approved files under `docs/adr/` if present
4. `docs/DOMAIN_RULES_V1.md`
5. `docs/POSTGRESQL_ERD_V1.md`
6. `docs/LEGACY_MAPPING_V1.md`
7. `docs/WEB_DONUSUM_MASTER_PLANI_V1.md`
8. `docs/CODEX_ANALYSIS_V1.md` as review/gap analysis only
9. `legacy/` as read-only AS-IS evidence

Never silently resolve a conflict by inventing a business rule.

## 2. Legacy is read-only

- Never modify, rename, format, delete, or generate files under `legacy/`.
- Legacy code is evidence of AS-IS behavior, not a target architecture template.
- Do not port WinForms patterns, CSV locking, Outlook draft logic, updater/launcher logic, or SQL Server `Schema.sql` directly into the web application.
- When a claim depends on legacy behavior, reference the source file/class/method in implementation reports or ADR notes.

## 3. Approved baseline

- Python 3.13.x
- Django 5.2 LTS series, exact patch pinned in dependency lock
- PostgreSQL 18.x
- Django Templates + HTMX + Alpine.js
- Celery + Redis for background jobs
- pytest + pytest-django
- Docker Compose for local/integration environment
- Production authentication direction: Authentik/OIDC
- `USE_TZ=True`, `TIME_ZONE="Europe/Istanbul"`

Do not add React/Vue unless a later approved ADR explicitly requires it.
Do not use SQLite as runtime or test fallback.

## 4. Repository direction

Preferred layout:

```text
web/
  manage.py
  config/
  apps/
    accounts/
    audit/
    core/
    ...domain apps added only by their work package
  templates/
  static/
  tests/
legacy/          # READ ONLY
docs/
  adr/
```

Do not create empty domain apps merely to match the future list unless the current work package requires them.

## 5. Django model rules

- Use UUID primary keys for domain/application-owned entities unless an approved ADR says otherwise.
- Use real DB types: `boolean`, `numeric/DecimalField`, `date`, `timestamptz`/aware DateTimeField.
- Never store numeric/boolean values as strings for legacy compatibility.
- Never make PostgreSQL `Schema.sql` from the legacy project the target schema.
- Historical records should prefer snapshot + nullable/protected reference where renaming/deletion must not rewrite history.
- DB constraints protect approved invariants only. Do not encode pending TO-BE decisions as hard constraints.
- Do not add generic JSONB as a shortcut for a typed domain model unless the source-of-truth explicitly calls for staging/event payloads.

## 6. Decimal and time rules

- Quality calculations use Python `Decimal`, not binary `float`.
- Legacy raw numeric strings must be preserved during migration/staging.
- Do not use locale-dependent implicit parsing.
- All application datetimes are timezone-aware.
- Store event timestamps as PostgreSQL timestamptz via Django timezone-aware fields.
- Legacy explicitly-UTC fields parse as UTC; naive legacy timestamps use `Europe/Istanbul` unless an ADR/profile proves otherwise.

## 7. Authentication and authorization

- `AUTH_USER_MODEL` must be custom from the first project migration.
- Never migrate legacy passwords, hashes, salts, protected/plain credentials, including INO credentials.
- Production local password login is not the target authentication path.
- OIDC issuer+subject identity is implemented in WP-002.
- UUID/non-guessable IDs are not authorization.
- UI hiding is not authorization.
- Mutating and read services must enforce action permission and applicable object/scope rules server-side.

## 8. Service and domain logic

- Views/controllers should orchestrate HTTP concerns, not contain business rules.
- Do not put workflow/state-transition business logic in `Model.save()` overrides.
- Do not use Django signals for critical business workflows.
- State changes belong in explicit command/use-case/service functions.
- Critical transitions use `transaction.atomic()` and, when concurrency matters, appropriate row locking/current-state assertions.
- External side effects are not executed inside an uncommitted DB transaction.
- Notification-producing domains use transactional outbox architecture once introduced.
- Read-heavy dashboards use selectors/query services/read models instead of bloating transactional services.

## 9. Audit rules

- Audit is append-only from the application perspective.
- Never log passwords, tokens, connection strings, decrypt keys, attachment bodies, or unnecessary personal data.
- Include actor/correlation/request context when available.
- Domain-specific state histories belong to their aggregates; generic audit does not replace them.

## 10. File/security rules

- Do not store drawing binaries in PostgreSQL.
- Use Django Storage abstraction; storage keys are server-generated and are not raw user paths.
- Do not expose internal filesystem paths.
- Do not place legacy decrypt keys in the Django web process.
- Treat uploads, CSV text, CAD metadata, email HTML and legacy descriptions as untrusted input.
- Keep Django autoescape enabled.
- Mutation endpoints must use CSRF protection for session-authenticated browser flows.
- Secrets come from environment/secret management, never committed files.

## 11. Migration rules

- Legacy migration is staging -> transform -> load -> reconcile, never CSV directly into production tables without traceability.
- Import runs must be idempotent.
- Preserve source file hash, row number/raw representation, derived business key, target UUID map, warnings/rejects and reconciliation result.
- Do not merge flat legacy measurement `RecordId` rows into a guessed multi-eye parent based on time/lot/operator proximity.
- Do not double-import SQL Server snapshot and CSV as independent authoritative sources.
- Pending drawing/product uniqueness must wait for WP-000 profiling and ADR approval.

## 12. Frontend rules

- Server-rendered Django templates are the default.
- Use HTMX for partial updates and interaction where it reduces complexity.
- Use Alpine.js only for small client-side state.
- Business outcomes (OK/NOK, permissions, state transitions, calculations) must be recomputed/validated server-side; never trust a browser-computed result.
- Turkish UI text is allowed and expected; Python identifiers, DB fields, URLs and code symbols should be English and consistent.

## 13. Testing requirements

Every work package must add appropriate tests. Favor:

- pure unit tests for normalization/calculation/domain rules
- PostgreSQL integration tests for constraints/transactions
- concurrency tests for critical state transitions
- parameterized permission matrix tests
- migration fixture/idempotency/reject tests
- Playwright only for critical browser workflows when introduced

A work package is not complete while tests are failing or skipped without an explicit documented reason.

## 14. Dependency policy

- Prefer mature, maintained dependencies.
- Pin exact resolved versions in a lock file.
- Do not add a package when standard Django/Python is sufficient.
- Record material architectural dependencies in the implementation report.

## 15. Work-package discipline

Implement only the current WP.
Do not “helpfully” start the next domain.
Do not resolve pending ADRs by assumption.
If the current WP encounters a pending decision, leave a documented gate rather than encoding a speculative rule.

Before implementation:

1. Read the current WP prompt and required source-of-truth docs.
2. Inspect only the relevant legacy files needed for AS-IS evidence.
3. Write a short execution checklist in the task response/report.

After implementation:

1. Run formatting/linting configured by the project.
2. Run migrations on a clean PostgreSQL DB.
3. Run tests.
4. Verify `git diff -- legacy/` is empty.
5. Create/update the WP implementation report requested by the prompt.
6. Summarize changed files, commands run, test results, known gaps and next gates.

## 16. Forbidden shortcuts

Do not:

- use SQLite “temporarily”
- copy legacy SQL schema as target
- put business logic into views/templates/model save hooks
- use Django signals for core workflows
- use float for measurement tolerance decisions
- expose filesystem paths
- migrate credentials
- infer unknown relationships from timestamps alone
- add hard constraints for unresolved TO-BE rules
- modify legacy source
- implement multiple WPs in one task unless explicitly instructed
