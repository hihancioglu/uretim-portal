import uuid
import django.db.models.deletion
from django.conf import settings
from django.db import migrations, models

class Migration(migrations.Migration):
    initial = True
    dependencies = [migrations.swappable_dependency(settings.AUTH_USER_MODEL)]
    operations = [migrations.CreateModel(
        name="AuditEvent",
        fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)),
            ("occurred_at", models.DateTimeField(auto_now_add=True, editable=False)),
            ("actor_snapshot", models.CharField(blank=True, editable=False, max_length=255)),
            ("event_type", models.CharField(editable=False, max_length=160)),
            ("entity_type", models.CharField(blank=True, editable=False, max_length=160)),
            ("entity_id", models.CharField(blank=True, editable=False, max_length=255)),
            ("correlation_id", models.CharField(blank=True, editable=False, max_length=64)),
            ("ip_address", models.GenericIPAddressField(blank=True, editable=False, null=True)),
            ("user_agent", models.CharField(blank=True, editable=False, max_length=512)),
            ("metadata", models.JSONField(blank=True, default=dict, editable=False)),
            ("actor", models.ForeignKey(blank=True, editable=False, null=True, on_delete=django.db.models.deletion.SET_NULL, to=settings.AUTH_USER_MODEL)),
        ],
        options={"ordering": ("-occurred_at",)},
    ), migrations.AddIndex(model_name="auditevent", index=models.Index(fields=["event_type", "occurred_at"], name="audit_event_type_time"))]

