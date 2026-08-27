from django.contrib import admin
from .models import ControlPoint, ControlPointVersion


class ReadOnlyAdmin(admin.ModelAdmin):
    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False


@admin.register(ControlPoint)
class ControlPointAdmin(ReadOnlyAdmin):
    list_display = ("id", "drawing", "spc_key", "is_active", "created_at")


@admin.register(ControlPointVersion)
class ControlPointVersionAdmin(ReadOnlyAdmin):
    list_display = (
        "id",
        "control_point",
        "drawing_revision",
        "version_no",
        "measure_code",
        "is_active",
    )
