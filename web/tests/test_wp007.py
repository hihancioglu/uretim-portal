from decimal import Decimal

import pytest
from django.core.exceptions import PermissionDenied

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.control_points.models import ControlPoint, ControlPointVersion
from apps.control_points.services import revise_control_point
from apps.core.models import FileObject
from apps.drawings.models import Drawing, DrawingRevision
from apps.inspections.models import InspectionSession, Measurement, QualityResult
from apps.inspections.selectors import get_inspection_session
from apps.inspections.services import (
    InspectionError, calculate_measurement_result, cancel_inspection,
    close_inspection_eye, complete_eye_visual_phase, create_inspection_draft,
    create_visual_control, finalize_inspection, finish_measurement_phase,
    save_measurement, start_inspection, update_visual_control,
    validate_measurement_decimal,
)
from apps.products.models import Product

pytestmark = pytest.mark.django_db


def actor_for(role, username, scope="PLASTIC"):
    user = User.objects.create_user(username=username)
    assignment = UserRole.objects.create(user=user, role=Role.objects.get(code=role))
    seed_assignment_scopes(assignment)
    return user


@pytest.fixture
def domain():
    seed_authorization_baseline()
    actor = actor_for("quality_manager", "quality")
    product = Product.objects.create(tr_code="TR", product_name="P")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    file = FileObject.objects.create(storage_key="objects/wp007", original_name="x.pdf", mime_type="application/pdf", size_bytes=1, sha256="a" * 64)
    revision = DrawingRevision.objects.create(drawing=drawing, revision_code="A", primary_file=file, status="ACTIVE")
    return actor, product, drawing, revision


def point(revision, code="10", name="Dış Çap", mandatory=True, nominal="20.00000", lower="19.90000", upper="20.20000", sort=10):
    logical = ControlPoint.objects.create(drawing=revision.drawing, spc_key=f"SPC-{code}")
    nominal_value = Decimal(nominal)
    return ControlPointVersion.objects.create(control_point=logical, drawing_revision=revision, version_no=1, measure_code=code, measure_name=name, nominal=nominal_value, lower_tolerance=Decimal(lower) - nominal_value, upper_tolerance=Decimal(upper) - nominal_value, lower_limit=Decimal(lower), upper_limit=Decimal(upper), unit="mm", page_no=1, x_ratio=Decimal("0.250000"), y_ratio=Decimal("0.750000"), is_mandatory=mandatory, measurement_group="Çap", sample_frequency="Her Kontrol", is_critical=True, sort_no=sort)


@pytest.mark.parametrize("value,expected", [("19.90000","OK"),("19.90001","OK"),("20.00000","OK"),("20.20000","OK"),("19.89999","NOK"),("20.20001","NOK")])
def test_decimal_ok_nok_boundaries(value, expected):
    assert calculate_measurement_result(Decimal(value), Decimal("19.90000"), Decimal("20.20000")) == expected


@pytest.mark.parametrize("value", ["not-decimal", Decimal("1.000001"), Decimal("1000000000"), Decimal("NaN")])
def test_numeric_contract_rejects_invalid_or_unrepresentable(value):
    with pytest.raises(InspectionError):
        validate_measurement_decimal(value)


def test_competing_start_calls_freeze_once_without_duplicate_children(domain):
    actor, _, _, revision = domain
    draft = create_inspection_draft(actor=actor, drawing_revision=revision, declared_eye_count=3)
    assert draft.status == "DRAFT" and not draft.eyes.exists() and not draft.requirements.exists()
    with pytest.raises(InspectionError, match="aktif kontrol noktası"):
        start_inspection(actor=actor, session=draft)
    draft.refresh_from_db()
    assert draft.status == "DRAFT" and not draft.eyes.exists()
    frozen = point(revision)
    competing_request_session = InspectionSession.objects.get(pk=draft.pk)
    started = start_inspection(actor=actor, session=draft)
    assert started.status == "IN_PROGRESS"
    assert list(started.eyes.values_list("eye_no", flat=True)) == [1, 2, 3]
    assert started.requirements.get().control_point_version == frozen
    with pytest.raises(InspectionError):
        start_inspection(actor=actor, session=competing_request_session)
    assert started.eyes.count() == 3 and started.requirements.count() == 1


def test_snapshot_draft_update_and_definition_revision_immunity(domain):
    actor, _, _, revision = domain
    old = point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    requirement, eye = session.requirements.get(), session.eyes.get()
    technical = actor_for("technical_drawing", "revision-author")
    revised = revise_control_point(actor=technical, control_point=old.control_point, drawing_revision=revision, data={
        "measure_code": "10", "measure_name": "Yeni Tanım", "nominal": Decimal("21.00000"),
        "lower_tolerance": Decimal("0.10000"), "upper_tolerance": Decimal("0.20000"),
        "unit": "mm", "page_no": 2, "x_ratio": Decimal("0.500000"),
        "y_ratio": Decimal("0.600000"), "is_mandatory": True,
        "measurement_group": "Yeni", "sample_frequency": "Yeni Sıklık",
        "is_critical": False, "sort_no": 99, "change_reason": "test revision",
    })
    old.refresh_from_db()
    requirement.refresh_from_db()
    assert not old.is_active and revised.is_active
    assert revised.control_point_id == old.control_point_id
    assert requirement.control_point_version_id == old.id
    assert revised.control_point.spc_key == old.control_point.spc_key
    measurement = save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20.20001"))
    assert measurement.result == "NOK"
    assert {
        "code": measurement.measure_code_snapshot,
        "name": measurement.measure_name_snapshot,
        "group": measurement.group_snapshot,
        "frequency": measurement.sample_frequency_snapshot,
        "critical": measurement.is_critical_snapshot,
        "sort": measurement.sort_no_snapshot,
        "nominal": measurement.nominal_snapshot,
        "lower": measurement.lower_limit_snapshot,
        "upper": measurement.upper_limit_snapshot,
        "unit": measurement.unit_snapshot,
        "page": measurement.page_no_snapshot,
        "x": measurement.x_ratio_snapshot,
        "y": measurement.y_ratio_snapshot,
        "spc": measurement.spc_key_snapshot,
        "version": measurement.measure_version_snapshot,
    } == {
        "code": "10", "name": "Dış Çap", "group": "Çap",
        "frequency": "Her Kontrol", "critical": True, "sort": 10,
        "nominal": Decimal("20.00000"), "lower": Decimal("19.90000"),
        "upper": Decimal("20.20000"), "unit": "mm", "page": 1,
        "x": Decimal("0.250000"), "y": Decimal("0.750000"),
        "spc": "SPC-10", "version": 1,
    }
    updated = save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20.00000"), note="düzeltildi")
    assert updated.pk == measurement.pk and updated.result == "OK" and Measurement.objects.count() == 1
    finish_measurement_phase(actor=actor, session=session)
    with pytest.raises(InspectionError):
        save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))


def test_closed_eye_and_all_closed_completion(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    eye = close_inspection_eye(actor=actor, eye=session.eyes.get())
    assert eye.is_closed and eye.close_reason == "Göz Kapalı" and eye.closed_by == actor
    with pytest.raises(InspectionError):
        save_measurement(actor=actor, eye=eye, requirement=session.requirements.get(), measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=session)
    with pytest.raises(InspectionError):
        create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result=QualityResult.OK)
    completed = finalize_inspection(actor=actor, session=session)
    assert completed.status == "COMPLETED" and completed.overall_result is None


def test_mandatory_visual_overall_nok_and_cancellation_history(domain):
    actor, _, _, revision = domain
    point(revision, code="10", nominal="20.00000", lower="19.90000", upper="20.10000")
    point(revision, code="20", name="Boy", nominal="30.00000", lower="29.90000", upper="30.10000", sort=20)
    point(revision, code="30", name="Kontrol", mandatory=False, sort=30)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision, declared_eye_count=2, lot_no="LOT-001"))
    reqs = {r.control_point_version.measure_code:r for r in session.requirements.select_related("control_point_version")}
    eyes = list(session.eyes.all())
    eye_2_diameter = None
    for eye in eyes:
        measurement = save_measurement(actor=actor, eye=eye, requirement=reqs["10"], measured_value=Decimal("20.15000") if eye.eye_no == 2 else Decimal("20"))
        if eye.eye_no == 2:
            eye_2_diameter = measurement
    assert eye_2_diameter is not None
    assert eye_2_diameter.result == Measurement.Result.NOK
    with pytest.raises(InspectionError, match="Göz 1.*20"):
        finish_measurement_phase(actor=actor, session=session)
    for eye in eyes:
        save_measurement(actor=actor, eye=eye, requirement=reqs["20"], measured_value=Decimal("30"))
    finish_measurement_phase(actor=actor, session=session)
    for eye in eyes:
        create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result=QualityResult.OK)
        complete_eye_visual_phase(actor=actor, eye=eye)
    completed = finalize_inspection(actor=actor, session=session)
    assert completed.overall_result == QualityResult.NOK
    assert AuditEvent.objects.filter(event_type="inspection.completed", entity_id=str(session.pk)).exists()
    with pytest.raises(InspectionError):
        cancel_inspection(actor=actor, session=session)


def test_real_authorization_manager_history_but_no_mutation(domain):
    actor, _, _, revision = domain
    manager = actor_for("manager", "manager")
    session = create_inspection_draft(actor=actor, drawing_revision=revision)
    assert get_inspection_session(manager, session.id) == session
    with pytest.raises(PermissionDenied):
        create_inspection_draft(actor=manager, drawing_revision=revision)
    technical = actor_for("technical_drawing", "technical")
    with pytest.raises(PermissionDenied):
        create_inspection_draft(actor=technical, drawing_revision=revision)


def test_scope_authorization_is_enforced(domain):
    _, product, _, revision = domain
    incoming = actor_for("incoming_quality", "incoming")
    plastic = actor_for("plastic_quality", "plastic")
    create_inspection_draft(actor=plastic, drawing_revision=revision)
    with pytest.raises(PermissionDenied):
        create_inspection_draft(actor=incoming, drawing_revision=revision)
    incoming_drawing = Drawing.objects.create(product=product, scope="INCOMING_QUALITY")
    incoming_revision = DrawingRevision.objects.create(drawing=incoming_drawing, revision_code="A", primary_file=revision.primary_file)
    create_inspection_draft(actor=incoming, drawing_revision=incoming_revision)
    with pytest.raises(PermissionDenied):
        create_inspection_draft(actor=plastic, drawing_revision=incoming_revision)


def test_eye_with_measurement_cannot_close_and_data_is_preserved(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    eye, requirement = session.eyes.get(), session.requirements.get()
    measurement = save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    with pytest.raises(InspectionError, match="Kayıt içeren"):
        close_inspection_eye(actor=actor, eye=eye)
    eye.refresh_from_db()
    assert not eye.is_closed
    assert Measurement.objects.get(pk=measurement.pk).measured_value == Decimal("20.00000")


def test_finalize_requires_visual_completion_for_open_eye(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    save_measurement(actor=actor, eye=session.eyes.get(), requirement=session.requirements.get(), measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=session)
    with pytest.raises(InspectionError, match="görsel kontrol tamamlanmadı"):
        finalize_inspection(actor=actor, session=session)
    session.refresh_from_db()
    assert session.status == InspectionSession.Status.WAITING_VISUAL


def test_visual_nok_sets_overall_nok_and_visual_update_locks_after_completion(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    eye, requirement = session.eyes.get(), session.requirements.get()
    save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=session)
    visual = create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result=QualityResult.OK)
    visual = update_visual_control(actor=actor, visual_control=visual, control_name="Yüzey kontrolü", result=QualityResult.NOK, note="kusur")
    assert visual.result == QualityResult.NOK and visual.note == "kusur"
    complete_eye_visual_phase(actor=actor, eye=eye)
    with pytest.raises(InspectionError, match="tamamlanmış"):
        update_visual_control(actor=actor, visual_control=visual, control_name="Değişmez", result=QualityResult.OK)
    completed = finalize_inspection(actor=actor, session=session)
    assert completed.status == InspectionSession.Status.COMPLETED
    assert completed.overall_result == QualityResult.NOK


@pytest.mark.parametrize("cancel_from", ["DRAFT", "IN_PROGRESS", "WAITING_VISUAL"])
def test_cancellation_from_each_mutable_state_preserves_children_and_blocks_writes(domain, cancel_from):
    actor, _, _, revision = domain
    version = point(revision)
    session = create_inspection_draft(actor=actor, drawing_revision=revision)
    eye = requirement = measurement = None
    if cancel_from != "DRAFT":
        session = start_inspection(actor=actor, session=session)
        eye, requirement = session.eyes.get(), session.requirements.get()
        measurement = save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    if cancel_from == "WAITING_VISUAL":
        session = finish_measurement_phase(actor=actor, session=session)
    cancelled = cancel_inspection(actor=actor, session=session, reason="test")
    assert cancelled.status == InspectionSession.Status.CANCELLED
    assert cancelled.cancelled_at and cancelled.cancelled_by == actor
    assert cancelled.requirements.count() == (0 if cancel_from == "DRAFT" else 1)
    if measurement:
        assert Measurement.objects.filter(pk=measurement.pk).exists()
        with pytest.raises(InspectionError):
            save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20.1"))
        with pytest.raises(InspectionError):
            create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result=QualityResult.OK)
        with pytest.raises(InspectionError):
            complete_eye_visual_phase(actor=actor, eye=eye)
    assert InspectionSession.objects.get(pk=session.pk).status == InspectionSession.Status.CANCELLED
    assert version.control_point_id


def test_state_changes_are_rechecked_from_locked_session_not_stale_child(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    stale_eye, requirement = session.eyes.select_related("session").get(), session.requirements.get()
    cancel_inspection(actor=actor, session=session)
    assert stale_eye.session.status == InspectionSession.Status.IN_PROGRESS
    with pytest.raises(InspectionError):
        save_measurement(actor=actor, eye=stale_eye, requirement=requirement, measured_value=Decimal("20"))


def test_visual_mutation_rechecks_locked_session_after_cancellation(domain):
    actor, _, _, revision = domain
    point(revision)
    session = start_inspection(actor=actor, session=create_inspection_draft(actor=actor, drawing_revision=revision))
    eye, requirement = session.eyes.get(), session.requirements.get()
    save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=session)
    stale_eye = session.eyes.select_related("session").get()
    visual = create_visual_control(actor=actor, eye=stale_eye, control_name="Yüzey", result=QualityResult.OK)
    cancel_inspection(actor=actor, session=session)
    assert stale_eye.session.status == InspectionSession.Status.WAITING_VISUAL
    with pytest.raises(InspectionError):
        create_visual_control(actor=actor, eye=stale_eye, control_name="Yüzey", result=QualityResult.OK)
    with pytest.raises(InspectionError):
        update_visual_control(actor=actor, visual_control=visual, control_name="Yüzey", result=QualityResult.NOK)
