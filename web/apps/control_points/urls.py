from django.urls import path
from . import views

app_name = "control_points"
urlpatterns = [
    path("revisions/<uuid:revision_id>/", views.version_list, name="list"),
    path("revisions/<uuid:revision_id>/create/", views.create, name="create"),
    path("revisions/<uuid:revision_id>/copy/", views.copy_to_revision, name="copy"),
    path("revisions/<uuid:revision_id>/<uuid:point_id>/", views.detail, name="detail"),
    path(
        "revisions/<uuid:revision_id>/<uuid:point_id>/revise/",
        views.revise,
        name="revise",
    ),
    path(
        "revisions/<uuid:revision_id>/<uuid:point_id>/deactivate/",
        views.deactivate,
        name="deactivate",
    ),
]
