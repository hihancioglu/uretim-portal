from django.conf import settings
from mozilla_django_oidc.auth import OIDCAuthenticationBackend
from .identity import IdentityDenied, resolve_oidc_identity

class AuthentikOIDCBackend(OIDCAuthenticationBackend):
    def verify_claims(self, claims):
        return bool(claims.get("sub") and claims.get("iss") == settings.OIDC_ISSUER_URL)

    def filter_users_by_claims(self, claims):
        return self.UserModel.objects.none()

    def create_user(self, claims):
        return resolve_oidc_identity(issuer=claims["iss"], subject=claims["sub"], claims=claims)

    def update_user(self, user, claims):
        return resolve_oidc_identity(issuer=claims["iss"], subject=claims["sub"], claims=claims)

    def authenticate(self, request, **kwargs):
        try:
            return super().authenticate(request, **kwargs)
        except IdentityDenied:
            return None
