import uuid

from django.conf import settings
from django.db import models

from apps.core.models import FileObject
from apps.products.models import Product


class Drawing(models.Model):
    class Scope(models.TextChoices):
        PLASTIC = "PLASTIC", "Plastik Resmi"
        INCOMING_QUALITY = "INCOMING_QUALITY", "Giriş Kalite Kontrol Resmi"
        TR = "TR", "TR Resmi"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    product = models.ForeignKey(
        Product, on_delete=models.PROTECT, related_name="drawings"
    )
    scope = models.CharField(max_length=32, choices=Scope)
    title = models.CharField(max_length=255, blank=True)
    is_active = models.BooleanField(default=True, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="drawings_created",
    )
    updated_at = models.DateTimeField(auto_now=True)
    updated_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="drawings_updated",
    )


class DrawingRevision(models.Model):
    class Status(models.TextChoices):
        DRAFT = "DRAFT", "Taslak"
        ACTIVE = "ACTIVE", "Aktif"
        SUPERSEDED = "SUPERSEDED", "Geçersiz Kılındı"
        WITHDRAWN = "WITHDRAWN", "Geri Çekildi"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    drawing = models.ForeignKey(
        Drawing, on_delete=models.PROTECT, related_name="revisions"
    )
    revision_code = models.CharField(max_length=120)
    status = models.CharField(max_length=16, choices=Status, default=Status.DRAFT)
    primary_file = models.ForeignKey(
        FileObject, on_delete=models.PROTECT, related_name="drawing_revisions"
    )
    change_reason = models.TextField(blank=True)
    effective_from = models.DateTimeField(null=True, blank=True)
    effective_to = models.DateTimeField(null=True, blank=True)
    approved_at = models.DateTimeField(null=True, blank=True)
    approved_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="drawing_revisions_approved",
    )
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="drawing_revisions_created",
    )
    updated_at = models.DateTimeField(auto_now=True)
    updated_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="drawing_revisions_updated",
    )

    class Meta:
        constraints = [
            models.UniqueConstraint(
                fields=("drawing",),
                condition=models.Q(status="ACTIVE"),
                name="drawings_one_active_revision",
            )
        ]
