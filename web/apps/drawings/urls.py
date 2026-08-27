from django.urls import path

from .views import revision_content, revision_file, revision_viewer
from . import management_views

app_name = "drawings"
urlpatterns = [
    path("manage/", management_views.management_home, name="manage-home"),
    path(
        "manage/products/new/",
        management_views.product_create,
        name="manage-product-create",
    ),
    path(
        "manage/products/<uuid:product_id>/",
        management_views.product_detail,
        name="manage-product-detail",
    ),
    path(
        "manage/products/<uuid:product_id>/edit/",
        management_views.product_edit,
        name="manage-product-edit",
    ),
    path(
        "manage/products/<uuid:product_id>/deactivate/",
        management_views.product_deactivate,
        name="manage-product-deactivate",
    ),
    path(
        "manage/products/<uuid:product_id>/drawings/new/",
        management_views.drawing_create,
        name="manage-drawing-create",
    ),
    path(
        "manage/drawings/<uuid:drawing_id>/",
        management_views.drawing_detail,
        name="manage-drawing-detail",
    ),
    path(
        "manage/drawings/<uuid:drawing_id>/edit/",
        management_views.drawing_edit,
        name="manage-drawing-edit",
    ),
    path(
        "manage/drawings/<uuid:drawing_id>/deactivate/",
        management_views.drawing_deactivate_view,
        name="manage-drawing-deactivate",
    ),
    path(
        "manage/drawings/<uuid:drawing_id>/revisions/new/",
        management_views.revision_create,
        name="manage-revision-create",
    ),
    path(
        "manage/revisions/<uuid:revision_id>/edit/",
        management_views.revision_edit,
        name="manage-revision-edit",
    ),
    path(
        "manage/revisions/<uuid:revision_id>/replace-file/",
        management_views.revision_replace_file,
        name="manage-revision-replace-file",
    ),
    path(
        "manage/revisions/<uuid:revision_id>/activate/",
        management_views.revision_activate,
        name="manage-revision-activate",
    ),
    path(
        "manage/revisions/<uuid:revision_id>/withdraw/",
        management_views.revision_withdraw,
        name="manage-revision-withdraw",
    ),
    path("revisions/<uuid:revision_id>/view/", revision_viewer, name="revision-viewer"),
    path(
        "revisions/<uuid:revision_id>/content/",
        revision_content,
        name="revision-content",
    ),
    path("revisions/<uuid:revision_id>/file/", revision_file, name="revision-file"),
]
