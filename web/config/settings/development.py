import os
os.environ.setdefault("DJANGO_SECRET_KEY", "development-only-not-for-production")
os.environ.setdefault("POSTGRES_PASSWORD", "development-only")
from .base import *  # noqa: F403,E402

DEBUG = True
ALLOWED_HOSTS = env_list("DJANGO_ALLOWED_HOSTS", "localhost,127.0.0.1")  # noqa: F405
SESSION_COOKIE_SECURE = os.getenv("DJANGO_SECURE_COOKIES", "false").lower() == "true"
CSRF_COOKIE_SECURE = SESSION_COOKIE_SECURE

