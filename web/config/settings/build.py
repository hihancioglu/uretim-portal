"""Build-only settings for management commands such as collectstatic."""

import os

os.environ.setdefault("DJANGO_SECRET_KEY", "build-only-not-for-production")
os.environ.setdefault("POSTGRES_PASSWORD", "build-only-not-for-production")

from .base import *  # noqa: E402,F403

DEBUG = False
ALLOWED_HOSTS = ["localhost"]
