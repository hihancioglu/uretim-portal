import uuid
from django.conf import settings
from django.db import models


class ControlPoint(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    drawing = models.ForeignKey(
        "drawings.Drawing", on_delete=models.PROTECT, related_name="control_points"
    )
    spc_key = models.CharField(max_length=120)
    logical_code = models.CharField(max_length=120, blank=True, null=True)
    is_active = models.BooleanField(default=True, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="control_points_created",
    )
    updated_at = models.DateTimeField(auto_now=True)
    updated_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="control_points_updated",
    )


class ControlPointVersion(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    control_point = models.ForeignKey(
        ControlPoint, on_delete=models.PROTECT, related_name="versions"
    )
    drawing_revision = models.ForeignKey(
        "drawings.DrawingRevision",
        on_delete=models.PROTECT,
        related_name="control_point_versions",
    )
    version_no = models.PositiveIntegerField()
    measure_code = models.CharField(max_length=120)
    measure_name = models.CharField(max_length=255)
    nominal = models.DecimalField(max_digits=14, decimal_places=5)
    lower_tolerance = models.DecimalField(max_digits=14, decimal_places=5)
    upper_tolerance = models.DecimalField(max_digits=14, decimal_places=5)
    lower_limit = models.DecimalField(max_digits=14, decimal_places=5)
    upper_limit = models.DecimalField(max_digits=14, decimal_places=5)
    unit = models.CharField(max_length=32, default="mm")
    page_no = models.PositiveIntegerField()
    x_ratio = models.DecimalField(max_digits=7, decimal_places=6)
    y_ratio = models.DecimalField(max_digits=7, decimal_places=6)
    is_mandatory = models.BooleanField(default=True)
    measurement_group = models.CharField(max_length=120, default="Genel")
    sample_frequency = models.CharField(max_length=120, default="Her Kontrol")
    is_critical = models.BooleanField(default=False)
    sort_no = models.IntegerField(default=0)
    valid_from = models.DateTimeField(null=True, blank=True)
    valid_to = models.DateTimeField(null=True, blank=True)
    change_reason = models.TextField(blank=True)
    is_active = models.BooleanField(default=True, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="control_point_versions_created",
    )
    updated_at = models.DateTimeField(auto_now=True)
    updated_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="control_point_versions_updated",
    )

    class Meta:
        ordering = ("sort_no", "measure_code", "id")
        constraints = [
            models.CheckConstraint(
                condition=models.Q(version_no__gte=1), name="cpv_version_no_gte_1"
            ),
            models.CheckConstraint(
                condition=models.Q(page_no__gte=1), name="cpv_page_no_gte_1"
            ),
            models.CheckConstraint(
                condition=models.Q(x_ratio__gte=0, x_ratio__lte=1),
                name="cpv_x_ratio_range",
            ),
            models.CheckConstraint(
                condition=models.Q(y_ratio__gte=0, y_ratio__lte=1),
                name="cpv_y_ratio_range",
            ),
            models.CheckConstraint(
                condition=models.Q(lower_tolerance__lte=0),
                name="cpv_lower_tol_nonpositive",
            ),
            models.CheckConstraint(
                condition=models.Q(upper_tolerance__gte=0),
                name="cpv_upper_tol_nonnegative",
            ),
            models.CheckConstraint(
                condition=models.Q(lower_limit__lte=models.F("nominal")),
                name="cpv_lower_limit_lte_nominal",
            ),
            models.CheckConstraint(
                condition=models.Q(upper_limit__gte=models.F("nominal")),
                name="cpv_upper_limit_gte_nominal",
            ),
            models.CheckConstraint(
                condition=models.Q(valid_to__isnull=True)
                | models.Q(valid_to__gte=models.F("valid_from")),
                name="cpv_valid_period",
            ),
            models.UniqueConstraint(
                fields=("control_point", "version_no"), name="cpv_point_version_unique"
            ),
            models.UniqueConstraint(
                fields=("control_point", "drawing_revision"),
                condition=models.Q(is_active=True),
                name="cpv_one_active_per_revision",
            ),
        ]
