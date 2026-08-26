from django.contrib import admin

from .models import FileObject


@admin.register(FileObject)
class FileObjectAdmin(admin.ModelAdmin):
    list_display = ("original_name", "mime_type", "size_bytes", "sha256", "created_at")
    search_fields = ("original_name", "sha256")

    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False
