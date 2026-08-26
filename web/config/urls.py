from django.urls import include, path

urlpatterns = [path("health/", include("apps.core.urls")), path("", include("apps.accounts.urls"))]

