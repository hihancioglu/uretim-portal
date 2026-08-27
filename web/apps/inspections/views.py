from django.contrib import messages
from django.core.exceptions import PermissionDenied, ValidationError
from django.core.paginator import Paginator
from django.db.models import Q
from django.http import JsonResponse
from django.shortcuts import get_object_or_404, redirect, render
from django.views.decorators.http import require_POST

from apps.accounts.authz import has_scoped_action
from apps.drawings.models import Drawing, DrawingRevision

from .forms import CorrectionForm, HistoryFilterForm, InspectionLaunchForm, MeasurementForm, VisualControlForm, WorkspaceQueryForm
from .models import InspectionEye, InspectionRequirement, InspectionSession, Measurement, VisualControl
from .selectors import get_inspection_session, list_inspection_sessions
from .services import (InspectionError, cancel_inspection, close_inspection_eye, complete_eye_visual_phase,
    correct_completed_measurement, create_and_start_inspection, create_visual_control, finalize_inspection,
    finish_measurement_phase, save_measurement, update_visual_control)


def _mutation_scopes(user):
    return [s for s in Drawing.Scope.values if has_scoped_action(user, "measurements.create", scope_type="DRAWING", scope_key=s)]


def _mutable_session(user, session_id):
    session = get_object_or_404(InspectionSession.objects.select_related("drawing_revision__drawing__product"), pk=session_id)
    if not has_scoped_action(user, "measurements.create", scope_type="DRAWING", scope_key=session.scope):
        raise PermissionDenied
    return session


def home(request):
    sessions = list_inspection_sessions(request.user)
    mutation_scopes = _mutation_scopes(request.user)
    return render(request, "inspections/home.html", {"ongoing": sessions.filter(status="IN_PROGRESS")[:10], "visual": sessions.filter(status="WAITING_VISUAL")[:10], "recent": sessions[:10], "can_create": bool(mutation_scopes), "mutation_scopes": mutation_scopes})


def launch(request):
    scopes = _mutation_scopes(request.user)
    if not scopes:
        raise PermissionDenied
    revisions = DrawingRevision.objects.filter(status=DrawingRevision.Status.ACTIVE, drawing__scope__in=scopes, drawing__is_active=True).select_related("drawing__product")
    query = request.GET.get("q", "").strip()
    if query:
        revisions = revisions.filter(Q(drawing__product__tr_code__icontains=query) | Q(drawing__product__product_name__icontains=query) | Q(drawing__product__plastic_code__icontains=query))
    form = InspectionLaunchForm(request.POST or None)
    if request.method == "POST" and form.is_valid():
        revision = get_object_or_404(revisions, pk=form.cleaned_data["drawing_revision"])
        try:
            session = create_and_start_inspection(actor=request.user, drawing_revision=revision, declared_eye_count=form.cleaned_data["declared_eye_count"], lot_no=form.cleaned_data["lot_no"], serial_no=form.cleaned_data["serial_no"])
            return redirect("inspections:work", session.id)
        except InspectionError as exc:
            form.add_error(None, str(exc))
    return render(request, "inspections/new.html", {"form": form, "revisions": revisions.order_by("drawing__product__tr_code", "revision_code"), "can_create": True})


def history(request):
    sessions = list_inspection_sessions(request.user)
    filter_form = HistoryFilterForm(request.GET)
    filter_form.is_valid()
    filters = {key: filter_form.cleaned_data.get(key, "") for key in filter_form.fields}
    if filters["q"]: sessions = sessions.filter(Q(drawing_revision__drawing__product__tr_code__icontains=filters["q"]) | Q(drawing_revision__drawing__product__product_name__icontains=filters["q"]))
    if filters["lot"]: sessions = sessions.filter(lot_no__icontains=filters["lot"])
    if filters["serial"]: sessions = sessions.filter(serial_no__icontains=filters["serial"])
    if filters["operator"]: sessions = sessions.filter(operator_name_snapshot__icontains=filters["operator"])
    if filters["status"]: sessions = sessions.filter(status=filters["status"])
    if filters["result"]: sessions = sessions.filter(overall_result=filters["result"])
    if filters["date_from"]: sessions = sessions.filter(created_at__date__gte=filters["date_from"])
    if filters["date_to"]: sessions = sessions.filter(created_at__date__lte=filters["date_to"])
    query_without_page = request.GET.copy()
    query_without_page.pop("page", None)
    mutation_scopes = _mutation_scopes(request.user)
    display_filters = {key: request.GET.get(key, "") for key in filter_form.fields}
    return render(request, "inspections/history.html", {"page": Paginator(sessions, 25).get_page(request.GET.get("page")), "filters": display_filters, "filter_form": filter_form, "statuses": InspectionSession.Status.choices, "query_without_page": query_without_page.urlencode(), "can_create": bool(mutation_scopes), "mutation_scopes": mutation_scopes})


def _detail_context(request, session, selected_eye=None):
    eyes = list(session.eyes.prefetch_related("measurements__revisions", "visual_controls").order_by("eye_no"))
    eye = selected_eye or (eyes[0] if eyes else None)
    requirements = list(session.requirements.select_related("control_point_version__control_point").order_by("control_point_version__sort_no", "control_point_version__measure_code", "id"))
    measurements = {m.requirement_id: m for m in (eye.measurements.all() if eye else [])}
    rows = [(r, measurements.get(r.id)) for r in requirements]
    mandatory_total = sum(1 for r in requirements if r.control_point_version.is_mandatory)
    mandatory_done = sum(1 for r, m in rows if r.control_point_version.is_mandatory and m)
    can_mutate = has_scoped_action(request.user, "measurements.create", scope_type="DRAWING", scope_key=session.scope)
    can_correct = has_scoped_action(request.user, "measurements.correct", scope_type="DRAWING", scope_key=session.scope)
    measurement_groups = sorted({r.control_point_version.measurement_group for r in requirements})
    return {"session": session, "eyes": eyes, "eye": eye, "rows": rows, "mandatory_total": mandatory_total, "mandatory_done": mandatory_done, "measurement_groups": measurement_groups, "can_mutate": can_mutate, "can_correct": can_correct, "can_create": bool(_mutation_scopes(request.user))}


def detail(request, session_id):
    session = get_inspection_session(request.user, session_id)
    return render(request, "inspections/detail.html", _detail_context(request, session))


def workspace(request, session_id):
    session = _mutable_session(request.user, session_id)
    query_form = WorkspaceQueryForm(request.GET)
    if not query_form.is_valid():
        return JsonResponse({"error": "Göz kimliği geçersiz."}, status=400)
    eye_id = query_form.cleaned_data.get("eye")
    eye = get_object_or_404(session.eyes, pk=eye_id) if eye_id else None
    return render(request, "inspections/workspace.html", _detail_context(request, session, eye))


def inspection_overlay(request, session_id, eye_id):
    session = get_object_or_404(InspectionSession, pk=session_id)
    can_read = has_scoped_action(request.user, "measurements.create", scope_type="DRAWING", scope_key=session.scope) or has_scoped_action(request.user, "measurements.view_history", scope_type="DRAWING", scope_key=session.scope)
    if not can_read:
        raise PermissionDenied
    eye = get_object_or_404(InspectionEye, pk=eye_id, session=session)
    values = {item.requirement_id: item.result for item in eye.measurements.only("requirement_id", "result")}
    requirements = session.requirements.select_related("control_point_version").order_by("control_point_version__sort_no", "control_point_version__measure_code", "id")
    return JsonResponse({"markers": [{"requirement_id": str(requirement.id), "page_no": requirement.control_point_version.page_no, "x_ratio": str(requirement.control_point_version.x_ratio), "y_ratio": str(requirement.control_point_version.y_ratio), "measure_code": requirement.control_point_version.measure_code, "measure_name": requirement.control_point_version.measure_name, "is_critical": requirement.control_point_version.is_critical, "state": values.get(requirement.id, "PENDING")} for requirement in requirements]})


def _safe_action(request, session_id, callback):
    session = _mutable_session(request.user, session_id)
    try: callback(session)
    except (InspectionError, ValidationError) as exc: messages.error(request, str(exc))
    return redirect("inspections:work", session.id)


@require_POST
def measurement_save(request, session_id, eye_id, requirement_id):
    session = _mutable_session(request.user, session_id)
    eye = get_object_or_404(InspectionEye, pk=eye_id, session=session)
    requirement = get_object_or_404(InspectionRequirement, pk=requirement_id, session=session)
    form = MeasurementForm(request.POST)
    if not form.is_valid(): return JsonResponse({"error": next(iter(form.errors.values()))[0]}, status=400)
    try:
        measurement = save_measurement(actor=request.user, eye=eye, requirement=requirement, **form.cleaned_data)
    except InspectionError as exc: return JsonResponse({"error": str(exc)}, status=409)
    return JsonResponse({"measurement_id": str(measurement.id), "value": str(measurement.measured_value), "result": measurement.result, "updated_at": measurement.updated_at.isoformat()})

@require_POST
def eye_close(request, session_id, eye_id): return _safe_action(request, session_id, lambda s: close_inspection_eye(actor=request.user, eye=get_object_or_404(InspectionEye, pk=eye_id, session=s), reason=request.POST.get("reason", "Göz Kapalı")))
@require_POST
def finish(request, session_id): return _safe_action(request, session_id, lambda s: finish_measurement_phase(actor=request.user, session=s))
@require_POST
def visual_create(request, session_id, eye_id):
    form = VisualControlForm(request.POST)
    return _safe_action(request, session_id, lambda s: create_visual_control(actor=request.user, eye=get_object_or_404(InspectionEye, pk=eye_id, session=s), **form.cleaned_data) if form.is_valid() else (_ for _ in ()).throw(InspectionError("Görsel kontrol verisi geçersiz.")))
@require_POST
def visual_update(request, session_id, eye_id, visual_id):
    form = VisualControlForm(request.POST)
    return _safe_action(request, session_id, lambda s: update_visual_control(actor=request.user, visual_control=get_object_or_404(VisualControl, pk=visual_id, eye_id=eye_id, eye__session=s), **form.cleaned_data) if form.is_valid() else (_ for _ in ()).throw(InspectionError("Görsel kontrol verisi geçersiz.")))
@require_POST
def visual_complete(request, session_id, eye_id): return _safe_action(request, session_id, lambda s: complete_eye_visual_phase(actor=request.user, eye=get_object_or_404(InspectionEye, pk=eye_id, session=s)))
@require_POST
def finalize(request, session_id): return _safe_action(request, session_id, lambda s: finalize_inspection(actor=request.user, session=s))
@require_POST
def cancel(request, session_id): return _safe_action(request, session_id, lambda s: cancel_inspection(actor=request.user, session=s, reason=request.POST.get("reason", "")))


def correct(request, session_id, measurement_id):
    session = get_inspection_session(request.user, session_id)
    if not has_scoped_action(request.user, "measurements.correct", scope_type="DRAWING", scope_key=session.scope): raise PermissionDenied
    measurement = get_object_or_404(Measurement.objects.select_related("eye", "measured_by"), pk=measurement_id, eye__session=session)
    form = CorrectionForm(request.POST or None)
    if request.method == "POST" and form.is_valid():
        try:
            correct_completed_measurement(actor=request.user, measurement=measurement, **form.cleaned_data)
            return redirect("inspections:detail", session.id)
        except InspectionError as exc: form.add_error(None, str(exc))
    return render(request, "inspections/correct.html", {"session": session, "measurement": measurement, "form": form, "can_create": bool(_mutation_scopes(request.user))})
