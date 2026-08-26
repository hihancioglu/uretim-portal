import uuid

from django.conf import settings
from django.core.validators import MinValueValidator
from django.db import models


class TrackedModel(models.Model):
    id = models.UUIDField(primary_key=True, default=uuid.uuid4, editable=False)
    is_active = models.BooleanField(default=True, db_index=True)
    created_at = models.DateTimeField(auto_now_add=True)
    created_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="%(app_label)s_%(class)s_created",
    )
    updated_at = models.DateTimeField(auto_now=True)
    updated_by = models.ForeignKey(
        settings.AUTH_USER_MODEL,
        null=True,
        blank=True,
        on_delete=models.PROTECT,
        related_name="%(app_label)s_%(class)s_updated",
    )

    class Meta:
        abstract = True


class Product(TrackedModel):
    tr_code = models.CharField(max_length=120, db_index=True)
    product_name = models.CharField(max_length=255, db_index=True)
    plastic_code = models.CharField(max_length=120, blank=True, db_index=True)
    material = models.CharField(max_length=255, blank=True)
    color_name = models.CharField(max_length=160, blank=True)
    molds = models.ManyToManyField(
        "Mold", through="ProductMold", related_name="products"
    )

    class Meta:
        ordering = ("tr_code", "product_name", "id")

    def __str__(self):
        return f"{self.tr_code} — {self.product_name}"


class Mold(TrackedModel):
    mold_code = models.CharField(max_length=120, db_index=True)
    description = models.TextField(blank=True)
    cavity_count = models.PositiveIntegerField(
        null=True, blank=True, validators=[MinValueValidator(1)]
    )

    class Meta:
        ordering = ("mold_code", "id")

    def __str__(self):
        return self.mold_code


class ProductMold(TrackedModel):
    product = models.ForeignKey(
        Product, on_delete=models.PROTECT, related_name="mold_links"
    )
    mold = models.ForeignKey(
        Mold, on_delete=models.PROTECT, related_name="product_links"
    )

    class Meta:
        ordering = ("product_id", "mold_id", "id")
        indexes = [
            models.Index(
                fields=("product", "is_active"), name="products_pm_product_active"
            ),
            models.Index(fields=("mold", "is_active"), name="products_pm_mold_active"),
        ]

    def __str__(self):
        return f"{self.product} / {self.mold}"
