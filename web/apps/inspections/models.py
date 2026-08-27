import uuid

from django.conf import settings
from django.db import models
from django.utils import timezone


class QualityResult(models.TextChoices):
    OK = "OK", "OK"
    NOK = "NOK", "NOK"


class InspectionSession(models.Model):
    class Status(models.TextChoices):
        DRAFT = "DRAFT", "Taslak"
        IN_PROGRESS = "IN_PROGRESS", "Devam Ediyor"
        WAITING_VISUAL = "WAITING_VISUAL", "Görsel Kontrol Bekliyor"
        COMPLETED = "COMPLETED", "Tamamlandı"
        CANCELLED = "CANCELLED", "İptal Edildi"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    drawing_revision = models.ForeignKey("drawings.DrawingRevision", on_delete=models.PROTECT, related_name="inspection_sessions")
    scope = models.CharField(max_length=32)
    status = models.CharField(max_length=20, choices=Status, default=Status.DRAFT, db_index=True)
    lot_no = models.CharField(max_length=120, blank=True, null=True)
    serial_no = models.CharField(max_length=120, blank=True, null=True)
    declared_eye_count = models.PositiveIntegerField(default=1)
    operator = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="inspection_sessions_operated")
    operator_name_snapshot = models.CharField(max_length=255, blank=True)
    started_at = models.DateTimeField(null=True, blank=True)
    completed_at = models.DateTimeField(null=True, blank=True)
    overall_result = models.CharField(max_length=3, choices=QualityResult, null=True, blank=True)
    legacy_record_id = models.CharField(max_length=255, null=True, blank=True)
    cancelled_at = models.DateTimeField(null=True, blank=True)
    cancelled_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="inspection_sessions_cancelled")
    cancelled_by_snapshot = models.CharField(max_length=255, blank=True)
    cancel_reason = models.TextField(blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        ordering = ("-created_at", "id")
        constraints = [
            models.CheckConstraint(condition=models.Q(declared_eye_count__gte=1), name="inspection_eye_count_gte_1"),
            models.CheckConstraint(condition=models.Q(completed_at__isnull=True) | models.Q(started_at__isnull=True) | models.Q(completed_at__gte=models.F("started_at")), name="inspection_completed_after_started"),
        ]


class InspectionRequirement(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    session = models.ForeignKey(InspectionSession, on_delete=models.PROTECT, related_name="requirements")
    control_point_version = models.ForeignKey("control_points.ControlPointVersion", on_delete=models.PROTECT, related_name="inspection_requirements")
    created_at = models.DateTimeField(auto_now_add=True)

    class Meta:
        constraints = [models.UniqueConstraint(fields=("session", "control_point_version"), name="inspection_requirement_unique")]


class InspectionEye(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    session = models.ForeignKey(InspectionSession, on_delete=models.PROTECT, related_name="eyes")
    eye_no = models.PositiveIntegerField()
    is_closed = models.BooleanField(default=False)
    close_reason = models.TextField(blank=True, null=True)
    closed_at = models.DateTimeField(null=True, blank=True)
    closed_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="inspection_eyes_closed")
    closed_by_snapshot = models.CharField(max_length=255, blank=True)
    visual_completed_at = models.DateTimeField(null=True, blank=True)
    visual_completed_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="inspection_eye_visuals_completed")
    visual_completed_by_snapshot = models.CharField(max_length=255, blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        ordering = ("eye_no",)
        constraints = [
            models.CheckConstraint(condition=models.Q(eye_no__gte=1), name="inspection_eye_no_gte_1"),
            models.UniqueConstraint(fields=("session", "eye_no"), name="inspection_eye_unique"),
        ]


class Measurement(models.Model):
    class Result(models.TextChoices):
        OK = "OK", "OK"
        NOK = "NOK", "NOK"
        ERROR = "ERROR", "Hata"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    eye = models.ForeignKey(InspectionEye, on_delete=models.PROTECT, related_name="measurements")
    requirement = models.ForeignKey(InspectionRequirement, on_delete=models.PROTECT, related_name="measurements")
    measured_value = models.DecimalField(max_digits=14, decimal_places=5)
    result = models.CharField(max_length=5, choices=Result)
    note = models.TextField(blank=True)
    measured_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="measurements_recorded")
    measured_by_snapshot = models.CharField(max_length=255, blank=True)
    measured_at = models.DateTimeField()
    measure_code_snapshot = models.CharField(max_length=120)
    measure_name_snapshot = models.CharField(max_length=255)
    group_snapshot = models.CharField(max_length=120)
    sample_frequency_snapshot = models.CharField(max_length=120)
    is_critical_snapshot = models.BooleanField()
    sort_no_snapshot = models.IntegerField()
    nominal_snapshot = models.DecimalField(max_digits=14, decimal_places=5)
    lower_limit_snapshot = models.DecimalField(max_digits=14, decimal_places=5)
    upper_limit_snapshot = models.DecimalField(max_digits=14, decimal_places=5)
    unit_snapshot = models.CharField(max_length=32)
    page_no_snapshot = models.PositiveIntegerField()
    x_ratio_snapshot = models.DecimalField(max_digits=7, decimal_places=6)
    y_ratio_snapshot = models.DecimalField(max_digits=7, decimal_places=6)
    spc_key_snapshot = models.CharField(max_length=120)
    measure_version_snapshot = models.PositiveIntegerField()
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    class Meta:
        constraints = [
            models.UniqueConstraint(fields=("eye", "requirement"), name="measurement_eye_requirement_unique"),
            models.CheckConstraint(condition=models.Q(lower_limit_snapshot__lte=models.F("upper_limit_snapshot")), name="measurement_limits_ordered"),
            models.CheckConstraint(condition=models.Q(page_no_snapshot__gte=1), name="measurement_page_gte_1"),
            models.CheckConstraint(condition=models.Q(x_ratio_snapshot__gte=0, x_ratio_snapshot__lte=1), name="measurement_x_ratio_range"),
            models.CheckConstraint(condition=models.Q(y_ratio_snapshot__gte=0, y_ratio_snapshot__lte=1), name="measurement_y_ratio_range"),
        ]


class VisualControl(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    eye = models.ForeignKey(InspectionEye, on_delete=models.PROTECT, related_name="visual_controls")
    control_name = models.CharField(max_length=255)
    result = models.CharField(max_length=3, choices=QualityResult)
    note = models.TextField(blank=True)
    controlled_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="visual_controls_recorded")
    controlled_by_snapshot = models.CharField(max_length=255, blank=True)
    controlled_at = models.DateTimeField()
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)


class MeasurementRevision(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    measurement = models.ForeignKey(Measurement, on_delete=models.PROTECT, related_name="revisions")
    revision_no = models.PositiveIntegerField()
    old_value = models.DecimalField(max_digits=14, decimal_places=5)
    new_value = models.DecimalField(max_digits=14, decimal_places=5)
    old_result = models.CharField(max_length=5, choices=Measurement.Result)
    new_result = models.CharField(max_length=5, choices=Measurement.Result)
    reason = models.CharField(max_length=500)
    changed_by = models.ForeignKey(settings.AUTH_USER_MODEL, null=True, blank=True, on_delete=models.PROTECT, related_name="measurement_corrections")
    changed_by_snapshot = models.CharField(max_length=255, blank=True)
    changed_at = models.DateTimeField(default=timezone.now)
    legacy_correction_id = models.CharField(max_length=255, null=True, blank=True)
    source_computer_name = models.CharField(max_length=255, null=True, blank=True)

    class Meta:
        ordering = ("measurement_id", "revision_no")
        constraints = [
            models.CheckConstraint(condition=models.Q(revision_no__gte=1), name="measurement_revision_no_gte_1"),
            models.UniqueConstraint(fields=("measurement", "revision_no"), name="measurement_revision_unique"),
        ]
