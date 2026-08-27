from apps.accounts.authz import require_scoped_action
from .models import ControlPointVersion


def list_active_versions_for_revision(actor, drawing_revision):
    require_scoped_action(
        actor,
        "drawings.view",
        scope_type="DRAWING",
        scope_key=drawing_revision.drawing.scope,
    )
    return (
        ControlPointVersion.objects.filter(
            drawing_revision=drawing_revision, is_active=True
        )
        .select_related("control_point", "created_by")
        .order_by("sort_no", "measure_code", "id")
    )


def list_active_versions_for_revision_page(actor, drawing_revision, page_no):
    if page_no < 1:
        raise ValueError("Sayfa numarası geçersiz.")
    return list_active_versions_for_revision(actor, drawing_revision).filter(
        page_no=page_no
    )


def list_version_history(actor, control_point):
    require_scoped_action(
        actor,
        "drawings.view",
        scope_type="DRAWING",
        scope_key=control_point.drawing.scope,
    )
    return control_point.versions.select_related(
        "drawing_revision", "created_by"
    ).order_by("-version_no")
