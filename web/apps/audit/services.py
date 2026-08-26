from typing import Any
from .models import AuditEvent

SENSITIVE_KEYS = {"password", "token", "secret", "authorization", "connection_string", "decrypt_key"}

def create_audit_event(*, event_type: str, metadata: dict[str, Any] | None = None, **context: Any) -> AuditEvent:
    values = metadata or {}
    if any(str(key).lower() in SENSITIVE_KEYS for key in values):
        raise ValueError("Audit metadata contains a prohibited sensitive key")
    return AuditEvent.objects.create(event_type=event_type, metadata=values, **context)

