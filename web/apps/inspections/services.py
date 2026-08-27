from decimal import Decimal

from django.db import transaction
from django.utils import timezone

from apps.accounts.authz import require_scoped_action
from apps.audit.services import create_audit_event
from apps.control_points.selectors import list_active_versions_for_revision

from .models import InspectionEye, InspectionRequirement, InspectionSession, Measurement, QualityResult, VisualControl


class InspectionError(Exception):
    pass


def _actor_name(actor):
    getter = getattr(actor, "get_full_name", None)
    return ((getter() if getter else "") or actor.get_username()).strip()


def _authorize(actor, session):
    require_scoped_action(actor, "measurements.create", scope_type="DRAWING", scope_key=session.scope)


def _audit(actor, event_type, obj, metadata=None):
    create_audit_event(actor=actor, actor_snapshot=_actor_name(actor), event_type=event_type, entity_type="inspection_session", entity_id=str(obj.pk), metadata=metadata or {})


def _lock_session(session):
    return InspectionSession.objects.select_for_update().select_related("drawing_revision__drawing").get(pk=session.pk)


def _lock_session_for_eye(eye):
    session_id = InspectionEye.objects.values_list("session_id", flat=True).get(pk=eye.pk)
    return _lock_session(InspectionSession(pk=session_id))


def _lock_eye(eye, session):
    return InspectionEye.objects.select_for_update().get(pk=eye.pk, session=session)


def _require_status(session, expected):
    if session.status != expected:
        raise InspectionError("Kontrol durumu bu işlem için uygun değil.")


def validate_measurement_decimal(value):
    if not isinstance(value, Decimal) or not value.is_finite():
        raise InspectionError("Ölçüm değeri geçersiz.")
    exponent = value.as_tuple().exponent
    integer_digits = max(value.adjusted() + 1, 0)
    if exponent < -5 or integer_digits > 9:
        raise InspectionError("Ölçüm değeri numeric(14,5) alanına tam olarak sığmıyor.")
    return value


def calculate_measurement_result(measured_value, lower_limit, upper_limit):
    for value in (measured_value, lower_limit, upper_limit):
        validate_measurement_decimal(value)
    if lower_limit > upper_limit:
        raise InspectionError("Ölçüm limitleri geçersiz.")
    return Measurement.Result.OK if lower_limit <= measured_value <= upper_limit else Measurement.Result.NOK


@transaction.atomic
def create_inspection_draft(*, actor, drawing_revision, declared_eye_count=1, lot_no="", serial_no=""):
    if not isinstance(declared_eye_count, int) or isinstance(declared_eye_count, bool) or declared_eye_count < 1:
        raise InspectionError("Göz sayısı en az 1 olmalıdır.")
    require_scoped_action(actor, "measurements.create", scope_type="DRAWING", scope_key=drawing_revision.drawing.scope)
    session = InspectionSession.objects.create(drawing_revision=drawing_revision, scope=drawing_revision.drawing.scope, declared_eye_count=declared_eye_count, lot_no=lot_no or "", serial_no=serial_no or "", operator=actor, operator_name_snapshot=_actor_name(actor))
    _audit(actor, "inspection.draft_created", session, {"drawing_revision_id": str(drawing_revision.pk)})
    return session


@transaction.atomic
def start_inspection(*, actor, session):
    locked = _lock_session(session)
    _require_status(locked, InspectionSession.Status.DRAFT)
    _authorize(actor, locked)
    versions = list(list_active_versions_for_revision(actor, locked.drawing_revision))
    if not versions:
        raise InspectionError("Bu teknik resimde aktif kontrol noktası bulunmuyor.")
    InspectionRequirement.objects.bulk_create([InspectionRequirement(session=locked, control_point_version=v) for v in versions])
    InspectionEye.objects.bulk_create([InspectionEye(session=locked, eye_no=n) for n in range(1, locked.declared_eye_count + 1)])
    locked.status = InspectionSession.Status.IN_PROGRESS
    locked.started_at = timezone.now()
    locked.save(update_fields=("status", "started_at", "updated_at"))
    _audit(actor, "inspection.started", locked, {"requirement_count": len(versions), "eye_count": locked.declared_eye_count})
    return locked


@transaction.atomic
def close_inspection_eye(*, actor, eye, reason="Göz Kapalı"):
    session = _lock_session_for_eye(eye)
    locked = _lock_eye(eye, session)
    _require_status(session, InspectionSession.Status.IN_PROGRESS)
    _authorize(actor, session)
    if locked.is_closed:
        raise InspectionError("Göz zaten kapalı.")
    if locked.measurements.exists() or locked.visual_controls.exists():
        raise InspectionError("Kayıt içeren göz kapatılamaz.")
    locked.is_closed = True
    locked.close_reason = reason
    locked.closed_at = timezone.now()
    locked.closed_by = actor
    locked.closed_by_snapshot = _actor_name(actor)
    locked.save(update_fields=("is_closed", "close_reason", "closed_at", "closed_by", "closed_by_snapshot", "updated_at"))
    _audit(actor, "inspection.eye_closed", session, {"eye_id": str(locked.pk), "eye_no": locked.eye_no})
    return locked


@transaction.atomic
def save_measurement(*, actor, eye, requirement, measured_value, note=""):
    session = _lock_session_for_eye(eye)
    locked_eye = _lock_eye(eye, session)
    _require_status(session, InspectionSession.Status.IN_PROGRESS)
    _authorize(actor, session)
    if locked_eye.is_closed:
        raise InspectionError("Kapalı göze ölçüm girilemez.")
    frozen = InspectionRequirement.objects.select_for_update().select_related("control_point_version__control_point").get(pk=requirement.pk)
    if locked_eye.session_id != frozen.session_id:
        raise InspectionError("Göz ve kontrol noktası aynı kontrole ait değil.")
    value = validate_measurement_decimal(measured_value)
    version = frozen.control_point_version
    result = calculate_measurement_result(value, version.lower_limit, version.upper_limit)
    snapshot = dict(measure_code_snapshot=version.measure_code, measure_name_snapshot=version.measure_name, group_snapshot=version.measurement_group, sample_frequency_snapshot=version.sample_frequency, is_critical_snapshot=version.is_critical, sort_no_snapshot=version.sort_no, nominal_snapshot=version.nominal, lower_limit_snapshot=version.lower_limit, upper_limit_snapshot=version.upper_limit, unit_snapshot=version.unit, page_no_snapshot=version.page_no, x_ratio_snapshot=version.x_ratio, y_ratio_snapshot=version.y_ratio, spc_key_snapshot=version.control_point.spc_key, measure_version_snapshot=version.version_no)
    now = timezone.now()
    measurement, created = Measurement.objects.get_or_create(eye=locked_eye, requirement=frozen, defaults={"measured_value": value, "result": result, "note": note, "measured_by": actor, "measured_by_snapshot": _actor_name(actor), "measured_at": now, **snapshot})
    if not created:
        measurement.measured_value = value
        measurement.result = result
        measurement.note = note
        measurement.measured_by = actor
        measurement.measured_by_snapshot = _actor_name(actor)
        measurement.measured_at = now
        measurement.save(update_fields=("measured_value", "result", "note", "measured_by", "measured_by_snapshot", "measured_at", "updated_at"))
    return measurement


def _first_missing(session):
    mandatory = list(session.requirements.filter(control_point_version__is_mandatory=True).select_related("control_point_version").order_by("control_point_version__sort_no", "control_point_version__measure_code", "id"))
    for eye in session.eyes.filter(is_closed=False).order_by("eye_no"):
        present = set(eye.measurements.values_list("requirement_id", flat=True))
        for requirement in mandatory:
            if requirement.id not in present:
                return eye, requirement
    return None


def _ensure_complete(session):
    missing = _first_missing(session)
    if missing:
        eye, requirement = missing
        version = requirement.control_point_version
        raise InspectionError(f"Göz {eye.eye_no} için zorunlu ölçüm eksik: {version.measure_code} — {version.measure_name}")


@transaction.atomic
def finish_measurement_phase(*, actor, session):
    locked = _lock_session(session)
    _require_status(locked, InspectionSession.Status.IN_PROGRESS)
    _authorize(actor, locked)
    _ensure_complete(locked)
    locked.status = InspectionSession.Status.WAITING_VISUAL
    locked.save(update_fields=("status", "updated_at"))
    _audit(actor, "inspection.measurement_phase_completed", locked)
    return locked


def _validate_visual(*, session, eye, actor):
    _require_status(session, InspectionSession.Status.WAITING_VISUAL)
    _authorize(actor, session)
    if eye.is_closed:
        raise InspectionError("Kapalı göze görsel kontrol girilemez.")
    if eye.visual_completed_at:
        raise InspectionError("Görsel kontrol aşaması tamamlanmış.")


@transaction.atomic
def create_visual_control(*, actor, eye, control_name, result, note=""):
    session = _lock_session_for_eye(eye)
    locked = _lock_eye(eye, session)
    _validate_visual(session=session, eye=locked, actor=actor)
    if result not in QualityResult.values:
        raise InspectionError("Görsel kontrol sonucu geçersiz.")
    if not control_name.strip():
        raise InspectionError("Görsel kontrol adı boş olamaz.")
    return VisualControl.objects.create(eye=locked, control_name=control_name.strip(), result=result, note=note, controlled_by=actor, controlled_by_snapshot=_actor_name(actor), controlled_at=timezone.now())


@transaction.atomic
def update_visual_control(*, actor, visual_control, control_name, result, note=""):
    eye_id = VisualControl.objects.values_list("eye_id", flat=True).get(pk=visual_control.pk)
    session_id = InspectionEye.objects.values_list("session_id", flat=True).get(pk=eye_id)
    session = _lock_session(InspectionSession(pk=session_id))
    eye = InspectionEye.objects.select_for_update().get(pk=eye_id, session=session)
    control = VisualControl.objects.select_for_update().get(pk=visual_control.pk, eye=eye)
    _validate_visual(session=session, eye=eye, actor=actor)
    if result not in QualityResult.values or not control_name.strip():
        raise InspectionError("Görsel kontrol verisi geçersiz.")
    control.control_name, control.result, control.note = control_name.strip(), result, note
    control.controlled_by, control.controlled_by_snapshot, control.controlled_at = actor, _actor_name(actor), timezone.now()
    control.save(update_fields=("control_name", "result", "note", "controlled_by", "controlled_by_snapshot", "controlled_at", "updated_at"))
    return control


@transaction.atomic
def complete_eye_visual_phase(*, actor, eye):
    session = _lock_session_for_eye(eye)
    locked = _lock_eye(eye, session)
    _require_status(session, InspectionSession.Status.WAITING_VISUAL)
    _authorize(actor, session)
    if locked.is_closed:
        raise InspectionError("Kapalı göz için görsel aşama gerekmez.")
    if locked.visual_completed_at:
        return locked
    locked.visual_completed_at, locked.visual_completed_by, locked.visual_completed_by_snapshot = timezone.now(), actor, _actor_name(actor)
    locked.save(update_fields=("visual_completed_at", "visual_completed_by", "visual_completed_by_snapshot", "updated_at"))
    _audit(actor, "inspection.eye_visual_completed", session, {"eye_id": str(locked.pk), "eye_no": locked.eye_no})
    return locked


@transaction.atomic
def finalize_inspection(*, actor, session):
    locked = _lock_session(session)
    _require_status(locked, InspectionSession.Status.WAITING_VISUAL)
    _authorize(actor, locked)
    _ensure_complete(locked)
    eyes = list(locked.eyes.select_for_update())
    open_eyes = [eye for eye in eyes if not eye.is_closed]
    for eye in open_eyes:
        if not eye.visual_completed_at:
            raise InspectionError(f"Göz {eye.eye_no} için görsel kontrol tamamlanmadı.")
    for eye in eyes:
        if eye.is_closed and (eye.measurements.exists() or eye.visual_controls.exists()):
            raise InspectionError("Kapalı gözde kontrol verisi bulunamaz.")
    nok = Measurement.objects.filter(eye__session=locked, result=Measurement.Result.NOK).exists() or VisualControl.objects.filter(eye__session=locked, result=QualityResult.NOK).exists()
    locked.overall_result = QualityResult.NOK if nok else (QualityResult.OK if open_eyes else None)
    locked.status, locked.completed_at = InspectionSession.Status.COMPLETED, timezone.now()
    locked.save(update_fields=("overall_result", "status", "completed_at", "updated_at"))
    _audit(actor, "inspection.completed", locked, {"overall_result": locked.overall_result})
    return locked


@transaction.atomic
def cancel_inspection(*, actor, session, reason=""):
    locked = _lock_session(session)
    if locked.status not in (InspectionSession.Status.DRAFT, InspectionSession.Status.IN_PROGRESS, InspectionSession.Status.WAITING_VISUAL):
        raise InspectionError("Bu kontrol iptal edilemez.")
    _authorize(actor, locked)
    locked.status, locked.cancelled_at, locked.cancelled_by = InspectionSession.Status.CANCELLED, timezone.now(), actor
    locked.cancelled_by_snapshot, locked.cancel_reason = _actor_name(actor), reason
    locked.save(update_fields=("status", "cancelled_at", "cancelled_by", "cancelled_by_snapshot", "cancel_reason", "updated_at"))
    _audit(actor, "inspection.cancelled", locked, {"reason": reason})
    return locked
