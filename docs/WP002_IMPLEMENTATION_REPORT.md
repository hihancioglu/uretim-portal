# WP-002 Implementation Report

## Execution checklist

Read the instructed source-of-truth/domain/ERD/mapping/reports and the relevant read-only AppState, AuthorizationService, login and user-management evidence; documented the authorization baseline first; implemented only accounts/OIDC/authz; added migrations/tests; ran available checks; confirmed the legacy diff.

## Implementation

`mozilla-django-oidc==4.0.1` was selected as the small maintained Django integration providing discovery, JWKS/signature, issuer/audience, state and nonce validation without custom JWT crypto. It is pinned with resolved HTTP/JOSE/cryptography dependencies. OIDC is disabled in dev/test by default and production fails fast for missing values/HTTP URLs. Minimal scopes are `openid profile email`; TLS verification and nonce remain enabled. Tokens/authorization codes/claims are never put in application models or audit metadata.

`ExternalIdentity` has UUID identity and unique `(issuer, subject)` plus minimal snapshots. Known active identities retain their user while snapshots/last-seen change. Inactive users and unknown identities are denied. Auto-provision defaults false; when enabled it refuses any email/preferred-username collision, generates a collision-resistant non-identity username, calls `set_unusable_password()`, and assigns no role. Explicit linking uses a token-free command. The normal portal has only OIDC login/callback/logout; no password form. Break-glass remains an IT deployment decision.

The application-owned models are Role, ActionPermission, RolePermission, UserRole and typed ScopeGrant. PROTECT/deactivation preserves assignments. Django groups/superuser are not business authorization. An idempotent data migration and `seed_authorization` command establish the documented 13 roles, 18 actions and evidence-backed grants without overwriting existing labels. Assignment creates only the documented drawing scopes. OIDC group synchronization is deferred.

Central `has/require_action`, `has/require_scoped_action`, and synthetic laboratory-context policy functions deny inactive components and require explicit scopes. Management commands link identity, assign/revoke role, and seed; Django admin provides inspection/controlled maintenance. Safe append-only events cover identity outcomes and assignment changes; subjects are fingerprinted and secrets/raw claims are excluded.

## Verification and gates

Commands: `python -m compileall -q web`; `git diff --check`; `git diff -- legacy/`; dependency install/index attempt; required Django/PostgreSQL/pytest/Docker commands where available. Local runtime verification is blocked because the host lacks Django/PostgreSQL/Docker and its package proxy returns HTTP 403. A GitHub CI run is also not available from the unpushed local branch. CI must confirm check, no migration drift, clean PostgreSQL migration, pytest and Docker build. No quality-domain model was added.

Authentik IT inputs and safe placeholders are documented in `.env.example` and `AUTHENTIK_OIDC_SETUP_RUNBOOK.md`; real issuer/client secret are not present.

`WP002_STATUS = BLOCKED`

**Precise technical blocker:** required runtime dependencies cannot be installed through the environment's 403 package proxy, Docker/PostgreSQL are absent, and the new commit has no GitHub Actions result; therefore the Definition of Done runtime/green-CI gates cannot truthfully be marked passed.
