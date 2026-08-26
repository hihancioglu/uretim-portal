import uuid

from django.conf import settings
from django.core.validators import RegexValidator
from django.db import models


class FileObject(models.Model):
    class Backend(models.TextChoices):
        FILESYSTEM = "FILESYSTEM", "Filesystem"

    class EncryptionScheme(models.TextChoices):
        NONE = "NONE", "None"
        LEGACY_AES_GCM = "LEGACY_AES_GCM", "Legacy AES-GCM"

    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    storage_backend = models.CharField(
        max_length=24, choices=Backend, default=Backend.FILESYSTEM
    )
    storage_key = models.CharField(max_length=255, unique=True, editable=False)
    original_name = models.CharField(max_length=255, editable=False)
    mime_type = models.CharField(max_length=255, editable=False)
    size_bytes = models.PositiveBigIntegerField(editable=False)
    sha256 = models.CharField(
        max_length=64,
        db_index=True,
        editable=False,
        validators=[RegexValidator(r"^[0-9a-f]{64}$")],
    )
    encryption_scheme = models.CharField(
        max_length=32,
        choices=EncryptionScheme,
        default=EncryptionScheme.NONE,
        editable=False,
    )
    encryption_key_version = models.CharField(
        max_length=32, null=True, blank=True, editable=False
    )
    created_at = models.DateTimeField(auto_now_add=True, editable=False)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        editable=False,
        related_name="core_file_objects_created",
    )

    class Meta:
        db_table = "core_file_object"
        constraints = [
            models.CheckConstraint(
                condition=models.Q(size_bytes__gt=0), name="core_file_size_positive"
            )
        ]

    def __str__(self):
        return self.original_name
