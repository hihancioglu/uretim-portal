# Üretim Portalı — WP-001 Platform Skeleton

This repository currently contains only the Django/PostgreSQL platform foundation. Quality-control domain applications are intentionally deferred.

## Prerequisites

- Docker with Compose v2
- For host execution: Python 3.13 and access to PostgreSQL 18 and Redis

## Local setup

```bash
cp .env.example .env
# Replace every placeholder and generate a strong DJANGO_SECRET_KEY.
docker compose config
docker compose up -d postgres redis
docker compose build
docker compose run --rm web python manage.py migrate --noinput
docker compose run --rm web pytest
docker compose up -d web worker nginx
curl http://localhost:8000/health/live
curl http://localhost:8000/health/ready
```

PostgreSQL and Redis are on an internal Compose network and have no host ports. The Nginx development entry point is `localhost:8000`; TLS and production orchestration are out of scope.

## Configuration

Select `config.settings.development`, `.test`, or `.production` through `DJANGO_SETTINGS_MODULE`. All database configurations use `django.db.backends.postgresql`; there is no SQLite fallback. Production settings require explicit hosts, trusted origins, secret, database credentials, and Redis/Celery URLs. A caller-supplied `X-Correlation-ID` is retained only when it is 1–64 ASCII letters, digits, `.`, `_`, `:`, or `-` and begins alphanumerically; otherwise a UUID is generated.

## Useful checks

```bash
docker compose run --rm web python manage.py check
docker compose run --rm web python manage.py makemigrations --check --dry-run
docker compose run --rm web celery -A config inspect ping
git diff -- legacy/
```

The `inspect ping` command requires a running worker. To verify import/start in isolation, run `timeout 10s docker compose run --rm worker celery -A config worker --loglevel=INFO` and expect timeout after the startup banner.
