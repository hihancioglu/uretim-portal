from django.core.exceptions import PermissionDenied
from django.http import JsonResponse
from django.shortcuts import get_object_or_404
from django.views.decorators.http import require_GET, require_POST
from apps.drawings.models import DrawingRevision
from .forms import ControlPointForm
from .models import ControlPoint
from .selectors import list_active_versions_for_revision_page, list_version_history
from .services import (
    ControlPointError,
    copy_control_points_to_revision,
    create_control_point,
    deactivate_control_point,
    revise_control_point,
)


def _revision(revision_id):
    return get_object_or_404(
        DrawingRevision.objects.select_related("drawing"), pk=revision_id
    )


def _version_json(version):
    return {
        "id": str(version.id),
        "control_point_id": str(version.control_point_id),
        "version_no": version.version_no,
        "measure_code": version.measure_code,
        "measure_name": version.measure_name,
        "nominal": str(version.nominal),
        "lower_tolerance": str(version.lower_tolerance),
        "upper_tolerance": str(version.upper_tolerance),
        "lower_limit": str(version.lower_limit),
        "upper_limit": str(version.upper_limit),
        "unit": version.unit,
        "page_no": version.page_no,
        "x_ratio": str(version.x_ratio),
        "y_ratio": str(version.y_ratio),
        "is_mandatory": version.is_mandatory,
        "measurement_group": version.measurement_group,
        "sample_frequency": version.sample_frequency,
        "is_critical": version.is_critical,
        "sort_no": version.sort_no,
        "change_reason": version.change_reason,
    }


def _error(exc, status=400):
    return JsonResponse(
        {"ok": False, "error": str(exc) or "Bu işlem için yetkiniz yok."}, status=status
    )


@require_GET
def version_list(request, revision_id):
    revision = _revision(revision_id)
    try:
        page_no = int(request.GET.get("page", "1"))
        points = list_active_versions_for_revision_page(request.user, revision, page_no)
        return JsonResponse({"points": [_version_json(item) for item in points]})
    except PermissionDenied as exc:
        return _error(exc, 403)
    except (ValueError, TypeError) as exc:
        return _error(exc)


@require_GET
def detail(request, revision_id, point_id):
    revision = _revision(revision_id)
    point = get_object_or_404(ControlPoint, pk=point_id, drawing=revision.drawing)
    try:
        active = point.versions.filter(
            drawing_revision=revision, is_active=True
        ).first()
        history = list_version_history(request.user, point)
        return JsonResponse(
            {
                "active": _version_json(active) if active else None,
                "spc_key": point.spc_key,
                "history": [
                    {
                        "version_no": v.version_no,
                        "measure_code": v.measure_code,
                        "revision": v.drawing_revision.revision_code,
                        "is_active": v.is_active,
                        "valid_from": v.valid_from.isoformat()
                        if v.valid_from
                        else None,
                        "valid_to": v.valid_to.isoformat() if v.valid_to else None,
                        "change_reason": v.change_reason,
                        "created_at": v.created_at.isoformat(),
                        "created_by": v.created_by.get_username()
                        if v.created_by
                        else "—",
                    }
                    for v in history
                ],
            }
        )
    except PermissionDenied as exc:
        return _error(exc, 403)


def _validated_form(request):
    form = ControlPointForm(request.POST)
    if not form.is_valid():
        raise ControlPointError(
            " ".join(
                message for messages in form.errors.values() for message in messages
            )
        )
    return form.cleaned_data


@require_POST
def create(request, revision_id):
    try:
        version = create_control_point(
            actor=request.user,
            drawing_revision=_revision(revision_id),
            data=_validated_form(request),
        )
        return JsonResponse({"ok": True, "point": _version_json(version)}, status=201)
    except PermissionDenied as exc:
        return _error(exc, 403)
    except ControlPointError as exc:
        return _error(exc)


@require_POST
def revise(request, revision_id, point_id):
    try:
        revision = _revision(revision_id)
        point = get_object_or_404(ControlPoint, pk=point_id)
        version = revise_control_point(
            actor=request.user,
            control_point=point,
            drawing_revision=revision,
            data=_validated_form(request),
        )
        return JsonResponse({"ok": True, "point": _version_json(version)})
    except PermissionDenied as exc:
        return _error(exc, 403)
    except ControlPointError as exc:
        return _error(exc)


@require_POST
def deactivate(request, revision_id, point_id):
    try:
        revision = _revision(revision_id)
        point = get_object_or_404(ControlPoint, pk=point_id)
        deactivate_control_point(
            actor=request.user, control_point=point, drawing_revision=revision
        )
        return JsonResponse({"ok": True})
    except PermissionDenied as exc:
        return _error(exc, 403)
    except ControlPointError as exc:
        return _error(exc)


@require_POST
def copy_to_revision(request, revision_id):
    try:
        target = _revision(revision_id)
        source = _revision(request.POST.get("source_revision_id"))
        copied = copy_control_points_to_revision(request.user, source, target)
        return JsonResponse({"ok": True, "count": len(copied)})
    except PermissionDenied as exc:
        return _error(exc, 403)
    except ControlPointError as exc:
        return _error(exc)
