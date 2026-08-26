import uuid
from django.conf import settings
from django.db import models


class AppendOnlyAuditQuerySet(models.QuerySet):
    def update(self, **kwargs):
        raise TypeError("Audit events are append-only")

    def delete(self):
        raise TypeError("Audit events are append-only")


class AuditEvent(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    occurred_at = models.DateTimeField(auto_now_add=True, editable=False)
    actor = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.SET_NULL, editable=False)
    actor_snapshot = models.CharField(max_length=255, blank=True, editable=False)
    event_type = models.CharField(max_length=160, editable=False)
    entity_type = models.CharField(max_length=160, blank=True, editable=False)
    entity_id = models.CharField(max_length=255, blank=True, editable=False)
    correlation_id = models.CharField(max_length=64, blank=True, editable=False)
    ip_address = models.GenericIPAddressField(null=True, blank=True, editable=False)
    user_agent = models.CharField(max_length=512, blank=True, editable=False)
    metadata = models.JSONField(default=dict, blank=True, editable=False)
    objects = AppendOnlyAuditQuerySet.as_manager()

    class Meta:
        ordering = ("-occurred_at",)
        indexes = [models.Index(fields=("event_type", "occurred_at"), name="audit_event_type_time")]

    def save(self, *args, **kwargs):
        if not self._state.adding:
            raise TypeError("Audit events are append-only")
        return super().save(*args, **kwargs)

    def delete(self, *args, **kwargs):
        raise TypeError("Audit events are append-only")
