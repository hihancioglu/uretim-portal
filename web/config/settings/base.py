import os
from pathlib import Path

BASE_DIR = Path(__file__).resolve().parents[2]


def env_list(name: str, default: str = "") -> list[str]:
    return [item.strip() for item in os.getenv(name, default).split(",") if item.strip()]


SECRET_KEY = os.environ["DJANGO_SECRET_KEY"]
DEBUG = False
ALLOWED_HOSTS = env_list("DJANGO_ALLOWED_HOSTS")
CSRF_TRUSTED_ORIGINS = env_list("DJANGO_CSRF_TRUSTED_ORIGINS")

INSTALLED_APPS = [
    "django.contrib.admin",
    "django.contrib.auth",
    "django.contrib.contenttypes",
    "django.contrib.sessions",
    "django.contrib.messages",
    "django.contrib.staticfiles",
    "mozilla_django_oidc",
    "apps.accounts",
    "apps.audit",
    "apps.core",
    "apps.products",
]
MIDDLEWARE = [
    "django.middleware.security.SecurityMiddleware",
    "apps.core.middleware.CorrelationIdMiddleware",
    "django.contrib.sessions.middleware.SessionMiddleware",
    "django.middleware.common.CommonMiddleware",
    "django.middleware.csrf.CsrfViewMiddleware",
    "django.contrib.auth.middleware.AuthenticationMiddleware",
    "django.contrib.messages.middleware.MessageMiddleware",
]
ROOT_URLCONF = "config.urls"
TEMPLATES = [{
    "BACKEND": "django.template.backends.django.DjangoTemplates",
    "DIRS": [BASE_DIR / "templates"],
    "APP_DIRS": True,
    "OPTIONS": {"context_processors": [
        "django.template.context_processors.request",
        "django.contrib.auth.context_processors.auth",
        "django.contrib.messages.context_processors.messages",
    ]},
}]
WSGI_APPLICATION = "config.wsgi.application"
DATABASES = {"default": {
    "ENGINE": "django.db.backends.postgresql",
    "NAME": os.getenv("POSTGRES_DB", "uretim_portal"),
    "USER": os.getenv("POSTGRES_USER", "uretim_portal"),
    "PASSWORD": os.environ["POSTGRES_PASSWORD"],
    "HOST": os.getenv("POSTGRES_HOST", "postgres"),
    "PORT": os.getenv("POSTGRES_PORT", "5432"),
    "CONN_MAX_AGE": 60,
}}
AUTH_PASSWORD_VALIDATORS = []
AUTH_USER_MODEL = "accounts.User"
LANGUAGE_CODE = "tr-tr"
TIME_ZONE = "Europe/Istanbul"
USE_I18N = True
USE_TZ = True
STATIC_URL = "static/"
STATIC_ROOT = BASE_DIR / "staticfiles"
DEFAULT_AUTO_FIELD = "django.db.models.BigAutoField"
REDIS_URL = os.getenv("REDIS_URL", "redis://redis:6379/0")
CELERY_BROKER_URL = os.getenv("CELERY_BROKER_URL", "redis://redis:6379/1")
CELERY_RESULT_BACKEND = os.getenv("CELERY_RESULT_BACKEND", "redis://redis:6379/2")
CELERY_BROKER_CONNECTION_RETRY_ON_STARTUP = True
LOGGING = {
    "version": 1,
    "disable_existing_loggers": False,
    "formatters": {"json": {"()": "apps.core.logging.JsonFormatter"}},
    "handlers": {"console": {"class": "logging.StreamHandler", "formatter": "json"}},
    "root": {"handlers": ["console"], "level": os.getenv("DJANGO_LOG_LEVEL", "INFO")},
}


OIDC_ENABLED = os.getenv("OIDC_ENABLED", "false").lower() == "true"
OIDC_AUTO_PROVISION = os.getenv("OIDC_AUTO_PROVISION", "false").lower() == "true"
OIDC_ISSUER_URL = os.getenv("OIDC_ISSUER_URL", "")
OIDC_RP_CLIENT_ID = os.getenv("OIDC_CLIENT_ID", "")
OIDC_RP_CLIENT_SECRET = os.getenv("OIDC_CLIENT_SECRET", "")
OIDC_OP_DISCOVERY_ENDPOINT = f"{OIDC_ISSUER_URL.rstrip('/')}/.well-known/openid-configuration" if OIDC_ISSUER_URL else ""
OIDC_RP_SCOPES = "openid profile email"
OIDC_USE_NONCE = True
OIDC_VERIFY_SSL = True
OIDC_POST_LOGOUT_REDIRECT_URI = os.getenv("OIDC_POST_LOGOUT_REDIRECT_URI", "/")
LOGIN_REDIRECT_URL = "/"
LOGIN_URL = "/login/"
AUTHENTICATION_BACKENDS = ["apps.accounts.oidc.AuthentikOIDCBackend"]
