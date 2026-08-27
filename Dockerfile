FROM node:24-alpine AS pdfjs-assets
WORKDIR /pdfjs
RUN npm init --yes >/dev/null && npm install \
    --ignore-scripts \
    --no-audit \
    --no-fund \
    --save-exact \
    pdfjs-dist@6.2.108

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
COPY --from=pdfjs-assets --chown=app:app \
    /pdfjs/node_modules/pdfjs-dist/build/pdf.mjs \
    /pdfjs/node_modules/pdfjs-dist/build/pdf.worker.mjs \
    /pdfjs/node_modules/pdfjs-dist/LICENSE \
    /app/web/apps/drawings/static/vendor/pdfjs/6.2.108/
RUN DJANGO_SETTINGS_MODULE=config.settings.build \
    python manage.py collectstatic --noinput

FROM nginx:1.29.2-alpine AS static-server
RUN sed -E -i \
    's#application/javascript([[:space:]]+)js;#application/javascript\1js mjs;#' \
    /etc/nginx/mime.types \
    && grep -Eq 'application/javascript[[:space:]]+js mjs;' /etc/nginx/mime.types
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=static-collector /app/web/staticfiles /srv/static

FROM application-base AS runtime
COPY --from=static-collector --chown=app:app /app/web/staticfiles /app/web/staticfiles
CMD ["gunicorn", "config.wsgi:application", "--bind=0.0.0.0:8000", "--access-logfile=-"]
