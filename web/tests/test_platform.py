import json
import uuid
from unittest.mock import Mock, patch
import pytest
from django.conf import settings
from django.contrib.auth import get_user_model
from django.core.management import call_command
from apps.audit.models import AuditEvent
from apps.audit.services import create_audit_event

def test_system_check():
    call_command("check")

def test_custom_user_model():
    assert settings.AUTH_USER_MODEL == "accounts.User"
    assert get_user_model()._meta.pk.get_internal_type() == "UUIDField"

def test_liveness_has_no_dependency_checks(client):
    with patch("apps.core.views.connection.ensure_connection") as db_check:
        response = client.get("/health/live")
    assert response.status_code == 200
    assert response.json() == {"status": "ok"}
    db_check.assert_not_called()

def test_readiness_success(client):
    redis_client = Mock()
    redis_client.ping.return_value = True
    with patch("apps.core.views.connection.ensure_connection"), patch("apps.core.views.connection.is_usable", return_value=True), patch("apps.core.views.Redis.from_url", return_value=redis_client):
        response = client.get("/health/ready")
    assert response.status_code == 200
    assert response.json()["checks"] == {"postgresql": True, "redis": True}

def test_readiness_failure(client):
    redis_client = Mock()
    redis_client.ping.side_effect = ConnectionError
    with patch("apps.core.views.connection.ensure_connection", side_effect=OSError), patch("apps.core.views.Redis.from_url", return_value=redis_client):
        response = client.get("/health/ready")
    assert response.status_code == 503
    assert response.json()["checks"] == {"postgresql": False, "redis": False}

def test_correlation_header_accepts_safe_value(client, caplog):
    response = client.get("/health/live", headers={"X-Correlation-ID": "safe-id_123"})
    assert response["X-Correlation-ID"] == "safe-id_123"
    assert any(getattr(record, "correlation_id", None) == "safe-id_123" for record in caplog.records)

def test_correlation_header_replaces_unsafe_value(client):
    response = client.get("/health/live", headers={"X-Correlation-ID": "unsafe value\n"})
    assert uuid.UUID(response["X-Correlation-ID"])

@pytest.mark.django_db
def test_audit_creation_is_append_only():
    event = create_audit_event(event_type="platform.test", metadata={"result": "ok"})
    assert event.metadata == {"result": "ok"}
    event.event_type = "changed"
    with pytest.raises(TypeError, match="append-only"):
        event.save()
    with pytest.raises(TypeError, match="append-only"):
        event.delete()
    with pytest.raises(TypeError, match="append-only"):
        AuditEvent.objects.filter(pk=event.pk).update(event_type="changed")
    with pytest.raises(TypeError, match="append-only"):
        AuditEvent.objects.filter(pk=event.pk).delete()

def test_audit_service_rejects_sensitive_metadata():
    with pytest.raises(ValueError, match="sensitive"):
        create_audit_event(event_type="platform.test", metadata={"token": "never-log-this"})
