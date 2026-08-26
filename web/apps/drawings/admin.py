from django.contrib import admin

from .models import Drawing, DrawingRevision


class InspectionOnlyAdmin(admin.ModelAdmin):
    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False


@admin.register(Drawing)
class DrawingAdmin(InspectionOnlyAdmin):
    list_display = ("product", "scope", "title", "is_active", "updated_at")
    list_filter = ("scope", "is_active")


@admin.register(DrawingRevision)
class DrawingRevisionAdmin(InspectionOnlyAdmin):
    list_display = (
        "drawing",
        "revision_code",
        "status",
        "effective_from",
        "updated_at",
    )
    list_filter = ("status",)
