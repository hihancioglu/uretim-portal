import re
from collections.abc import Mapping
from typing import Any

from .models import AuditEvent

SENSITIVE_KEYS = {
    "password",
    "passwd",
    "pwd",
    "token",
    "accesstoken",
    "authtoken",
    "bearertoken",
    "idtoken",
    "refreshtoken",
    "secret",
    "apisecret",
    "clientsecret",
    "authorization",
    "proxyauthorization",
    "connectionstring",
    "databaseurl",
    "decryptkey",
    "apikey",
    "privatekey",
    "secretkey",
}


def _normalise_key(key: object) -> str:
    """Make common case and separator variants comparable."""
    return re.sub(r"[^a-z0-9]", "", str(key).casefold())


def _contains_sensitive_key(value: Any) -> bool:
    if isinstance(value, Mapping):
        return any(
            _normalise_key(key) in SENSITIVE_KEYS or _contains_sensitive_key(item)
            for key, item in value.items()
        )
    if isinstance(value, (list, tuple)):
        return any(_contains_sensitive_key(item) for item in value)
    return False


def create_audit_event(*, event_type: str, metadata: dict[str, Any] | None = None, **context: Any) -> AuditEvent:
    values = metadata or {}
    if _contains_sensitive_key(values):
        raise ValueError("Audit metadata contains a prohibited sensitive key")
    return AuditEvent.objects.create(event_type=event_type, metadata=values, **context)
