from django.contrib import admin
from django.urls import include, path

urlpatterns = [
    path("admin/", admin.site.urls),
    path("health/", include("apps.core.urls")),
    path("drawings/", include("apps.drawings.urls")),
    path("", include("apps.accounts.urls")),
]
