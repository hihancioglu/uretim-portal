# Authentik OIDC setup runbook

Create a confidential standards-compliant OIDC application/provider. Register the exact HTTPS callback `/auth/callback/`, permit scopes `openid profile email`, and configure signing/discovery/JWKS normally. Do not grant offline access or add group claims for authorization.

Set secret-manager/environment values: `OIDC_ENABLED=true`, `OIDC_ISSUER_URL`, `OIDC_CLIENT_ID`, `OIDC_CLIENT_SECRET`, `OIDC_REDIRECT_URI`, `OIDC_POST_LOGOUT_REDIRECT_URI`, and normally `OIDC_AUTO_PROVISION=false`. Production rejects missing settings and non-HTTPS issuer/redirect. Pre-create users and run `python manage.py link_oidc_identity --username ... --issuer ... --subject ...`; then seed/assign business roles with controlled management commands. No token is accepted by these commands.

Django admin is a separate operational break-glass surface. IT must define its MFA/network/access lifecycle; no default credential is created and the portal exposes no password login form.
