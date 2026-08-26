from django.urls import include, path
from . import views
urlpatterns = [
    path("login/", views.login_start, name="login"),
    path("auth/", include("mozilla_django_oidc.urls")),
    path("logout/", views.logout_view, name="logout"),
    path("access-denied/", views.access_denied, name="access_denied"),
]
