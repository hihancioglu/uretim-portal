import os


REQUIRED_ENVIRONMENT = (
    "DJANGO_SECRET_KEY",
    "DJANGO_ALLOWED_HOSTS",
    "DJANGO_CSRF_TRUSTED_ORIGINS",
    "POSTGRES_DB",
    "POSTGRES_USER",
    "POSTGRES_PASSWORD",
    "POSTGRES_HOST",
    "POSTGRES_PORT",
    "REDIS_URL",
    "CELERY_BROKER_URL",
    "CELERY_RESULT_BACKEND",
    "DRAWING_STORAGE_ROOT",
)

missing = [name for name in REQUIRED_ENVIRONMENT if not os.environ.get(name, "").strip()]
if missing:
    raise RuntimeError(
        "Missing required production environment variables: " + ", ".join(missing)
    )

from .base import *  # noqa: E402,F403

if DRAWING_STORAGE_BACKEND != "filesystem":  # noqa: F405
    raise RuntimeError("WP-004 production supports only filesystem drawing storage")
drawing_root = Path(DRAWING_STORAGE_ROOT).resolve()  # noqa: F405
for forbidden_root in (BASE_DIR.resolve(), STATIC_ROOT.resolve()):  # noqa: F405
    if drawing_root == forbidden_root or drawing_root.is_relative_to(forbidden_root):
        raise RuntimeError("DRAWING_STORAGE_ROOT must be outside the checkout and public roots")
if DRAWING_MAX_UPLOAD_BYTES <= 0:  # noqa: F405
    raise RuntimeError("DRAWING_MAX_UPLOAD_BYTES must be positive")

if not ALLOWED_HOSTS:  # noqa: F405
    raise RuntimeError("DJANGO_ALLOWED_HOSTS must be set in production")
SECURE_SSL_REDIRECT = True
SECURE_PROXY_SSL_HEADER = ("HTTP_X_FORWARDED_PROTO", "https")
SESSION_COOKIE_SECURE = True
SESSION_COOKIE_HTTPONLY = True
SESSION_COOKIE_SAMESITE = "Lax"
CSRF_COOKIE_SECURE = True
CSRF_COOKIE_HTTPONLY = True
SECURE_HSTS_SECONDS = 31536000
SECURE_HSTS_INCLUDE_SUBDOMAINS = True
SECURE_HSTS_PRELOAD = True
SECURE_CONTENT_TYPE_NOSNIFF = True
X_FRAME_OPTIONS = "DENY"

if OIDC_ENABLED:  # noqa: F405
    oidc_required = ("OIDC_ISSUER_URL", "OIDC_CLIENT_ID", "OIDC_CLIENT_SECRET", "OIDC_REDIRECT_URI")
    oidc_missing = [name for name in oidc_required if not os.environ.get(name, "").strip()]
    if oidc_missing:
        raise RuntimeError("Missing required OIDC environment variables: " + ", ".join(oidc_missing))
    for name in ("OIDC_ISSUER_URL", "OIDC_REDIRECT_URI"):
        if not os.environ[name].startswith("https://"):
            raise RuntimeError(f"{name} must use HTTPS in production")
