FROM python:3.13.7-slim

ENV PYTHONDONTWRITEBYTECODE=1 PYTHONUNBUFFERED=1
WORKDIR /app
RUN addgroup --system app && adduser --system --ingroup app app
RUN install -d -o app -g app -m 0700 /data/drawings
COPY requirements.lock ./
RUN pip install --no-cache-dir -r requirements.lock
COPY --chown=app:app web ./web
USER app
WORKDIR /app/web
CMD ["gunicorn", "config.wsgi:application", "--bind=0.0.0.0:8000", "--access-logfile=-"]
