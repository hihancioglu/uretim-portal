from django.urls import path

from .views import revision_content, revision_file, revision_viewer

app_name = "drawings"
urlpatterns = [
    path("revisions/<uuid:revision_id>/view/", revision_viewer, name="revision-viewer"),
    path(
        "revisions/<uuid:revision_id>/content/",
        revision_content,
        name="revision-content",
    ),
    path("revisions/<uuid:revision_id>/file/", revision_file, name="revision-file")
]
