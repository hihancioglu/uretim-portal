from django.contrib import admin

from .models import InspectionEye, InspectionRequirement, InspectionSession, Measurement, MeasurementRevision, VisualControl


class InspectionReadOnlyAdmin(admin.ModelAdmin):
    def has_add_permission(self, request):
        return False

    def has_change_permission(self, request, obj=None):
        return False

    def has_delete_permission(self, request, obj=None):
        return False


admin.site.register(InspectionSession, InspectionReadOnlyAdmin)
admin.site.register(InspectionRequirement, InspectionReadOnlyAdmin)
admin.site.register(InspectionEye, InspectionReadOnlyAdmin)
admin.site.register(Measurement, InspectionReadOnlyAdmin)
admin.site.register(VisualControl, InspectionReadOnlyAdmin)
admin.site.register(MeasurementRevision, InspectionReadOnlyAdmin)
