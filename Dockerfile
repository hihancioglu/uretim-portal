FROM python:3.13.7-slim AS application-base

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
RUN addgroup --system app && adduser --system --ingroup app app
RUN install -d -o app -g app -m 0700 /data/drawings
COPY requirements.lock ./
RUN pip install --no-cache-dir -r requirements.lock
COPY --chown=app:app web ./web
USER app
WORKDIR /app/web

FROM application-base AS static-collector
RUN DJANGO_SETTINGS_MODULE=config.settings.production \
    DJANGO_SECRET_KEY=build-only POSTGRES_PASSWORD=build-only \
    DJANGO_ALLOWED_HOSTS=localhost DRAWING_STORAGE_ROOT=/data/drawings \
    python manage.py collectstatic --noinput

FROM nginx:1.29.2-alpine AS static-server
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=static-collector /app/web/staticfiles /srv/static

FROM application-base AS runtime
COPY --from=static-collector --chown=app:app /app/web/staticfiles /app/web/staticfiles
CMD ["gunicorn", "config.wsgi:application", "--bind=0.0.0.0:8000", "--access-logfile=-"]
