from django.contrib import admin
from django.urls import include, path

urlpatterns = [
    path("admin/", admin.site.urls),
    path("health/", include("apps.core.urls")),
    path("drawings/", include("apps.drawings.urls")),
    path("control-points/", include("apps.control_points.urls")),
    path("inspections/", include("apps.inspections.urls")),
    path("", include("apps.accounts.urls")),
]
