from django.urls import path

from .views import revision_file

app_name = "drawings"
urlpatterns = [
    path("revisions/<uuid:revision_id>/file/", revision_file, name="revision-file")
]
