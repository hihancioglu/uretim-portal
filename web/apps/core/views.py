from django.conf import settings
from django.db import connection
from django.http import JsonResponse
from redis import Redis

def live(_request):
    return JsonResponse({"status": "ok"})

def ready(_request):
    checks = {"postgresql": False, "redis": False}
    try:
        connection.ensure_connection()
        checks["postgresql"] = connection.is_usable()
    except Exception:
        checks["postgresql"] = False
    client = None
    try:
        client = Redis.from_url(settings.REDIS_URL, socket_connect_timeout=1, socket_timeout=1)
        checks["redis"] = bool(client.ping())
    except Exception:
        checks["redis"] = False
    finally:
        if client is not None:
            try:
                client.close()
            except Exception:
                checks["redis"] = False
    healthy = all(checks.values())
    return JsonResponse({"status": "ok" if healthy else "unavailable", "checks": checks}, status=200 if healthy else 503)
