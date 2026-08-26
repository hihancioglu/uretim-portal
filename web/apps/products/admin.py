from django.contrib import admin

from .models import Mold, Product, ProductMold


class InspectionOnlyAdmin(admin.ModelAdmin):
    """Admin is an inspection surface; audited mutations use domain services."""

    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False


@admin.register(Product)
class ProductAdmin(InspectionOnlyAdmin):
    list_display = (
        "tr_code",
        "product_name",
        "plastic_code",
        "is_active",
        "updated_at",
    )
    list_filter = ("is_active",)
    search_fields = ("tr_code", "product_name", "plastic_code")


@admin.register(Mold)
class MoldAdmin(InspectionOnlyAdmin):
    list_display = ("mold_code", "cavity_count", "is_active", "updated_at")
    list_filter = ("is_active",)
    search_fields = ("mold_code", "description")


@admin.register(ProductMold)
class ProductMoldAdmin(InspectionOnlyAdmin):
    list_display = ("product", "mold", "is_active", "updated_at")
    list_filter = ("is_active",)
    search_fields = ("product__tr_code", "mold__mold_code")
