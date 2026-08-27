import uuid

import django.db.models.deletion
import django.utils.timezone
from django.conf import settings
from django.db import migrations, models


class Migration(migrations.Migration):
    dependencies = [("inspections", "0001_initial"), migrations.swappable_dependency(settings.AUTH_USER_MODEL)]
    operations = [
        migrations.CreateModel(name="MeasurementRevision", fields=[
            ("id", models.UUIDField(default=uuid.uuid4, editable=False, primary_key=True, serialize=False)),
            ("revision_no", models.PositiveIntegerField()),
            ("old_value", models.DecimalField(decimal_places=5, max_digits=14)),
            ("new_value", models.DecimalField(decimal_places=5, max_digits=14)),
            ("old_result", models.CharField(choices=[("OK", "OK"), ("NOK", "NOK"), ("ERROR", "Hata")], max_length=5)),
            ("new_result", models.CharField(choices=[("OK", "OK"), ("NOK", "NOK"), ("ERROR", "Hata")], max_length=5)),
            ("reason", models.CharField(max_length=500)),
            ("changed_by_snapshot", models.CharField(blank=True, max_length=255)),
            ("changed_at", models.DateTimeField(default=django.utils.timezone.now)),
            ("legacy_correction_id", models.CharField(blank=True, max_length=255, null=True)),
            ("source_computer_name", models.CharField(blank=True, max_length=255, null=True)),
            ("changed_by", models.ForeignKey(blank=True, null=True, on_delete=django.db.models.deletion.PROTECT, related_name="measurement_corrections", to=settings.AUTH_USER_MODEL)),
            ("measurement", models.ForeignKey(on_delete=django.db.models.deletion.PROTECT, related_name="revisions", to="inspections.measurement")),
        ], options={"ordering": ("measurement_id", "revision_no")}),
        migrations.AddConstraint(model_name="measurementrevision", constraint=models.CheckConstraint(condition=models.Q(("revision_no__gte", 1)), name="measurement_revision_no_gte_1")),
        migrations.AddConstraint(model_name="measurementrevision", constraint=models.UniqueConstraint(fields=("measurement", "revision_no"), name="measurement_revision_unique")),
    ]
