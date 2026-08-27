from django.core.exceptions import PermissionDenied

from apps.accounts.authz import has_scoped_action, require_scoped_action
from apps.drawings.models import Drawing

from .models import InspectionSession


def _authorize(actor, session):
    require_scoped_action(actor, "measurements.view_history", scope_type="DRAWING", scope_key=session.scope)


def get_inspection_session(actor, session_id):
    session = InspectionSession.objects.select_related("drawing_revision__drawing__product", "operator", "cancelled_by").get(pk=session_id)
    _authorize(actor, session)
    return session


def list_inspection_sessions(actor, drawing_revision=None, product=None, status=None, lot_no=None, serial_no=None):
    scopes = [scope for scope in Drawing.Scope.values if has_scoped_action(actor, "measurements.view_history", scope_type="DRAWING", scope_key=scope)]
    if not scopes:
        raise PermissionDenied
    query = InspectionSession.objects.filter(scope__in=scopes).select_related("drawing_revision__drawing__product", "operator")
    if drawing_revision is not None:
        query = query.filter(drawing_revision=drawing_revision)
    if product is not None:
        query = query.filter(drawing_revision__drawing__product=product)
    if status is not None:
        query = query.filter(status=status)
    if lot_no is not None:
        query = query.filter(lot_no=lot_no)
    if serial_no is not None:
        query = query.filter(serial_no=serial_no)
    return query.order_by("-created_at", "id")


def list_session_requirements(actor, session):
    _authorize(actor, session)
    return session.requirements.select_related("control_point_version__control_point").order_by("control_point_version__sort_no", "control_point_version__measure_code", "id")


def list_session_eyes(actor, session):
    _authorize(actor, session)
    return session.eyes.select_related("closed_by", "visual_completed_by").prefetch_related("measurements", "visual_controls").order_by("eye_no")


def list_eye_measurements(actor, eye):
    _authorize(actor, eye.session)
    return eye.measurements.select_related("requirement__control_point_version__control_point", "measured_by").order_by("sort_no_snapshot", "measure_code_snapshot", "id")


def list_eye_visual_controls(actor, eye):
    _authorize(actor, eye.session)
    return eye.visual_controls.select_related("controlled_by").order_by("created_at", "id")
