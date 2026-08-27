from decimal import Decimal
from pathlib import Path

import pytest
from django.core.exceptions import PermissionDenied
from django.urls import reverse

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.control_points.services import revise_control_point
from apps.core.models import FileObject
from apps.drawings.models import Drawing, DrawingRevision
from apps.products.models import Product
from apps.inspections.forms import MeasurementForm
from apps.inspections.models import InspectionSession, Measurement, MeasurementRevision, QualityResult
from apps.inspections.services import (InspectionError, close_inspection_eye,
    complete_eye_visual_phase, correct_completed_measurement,
    create_and_start_inspection, create_visual_control, finalize_inspection,
    finish_measurement_phase, save_measurement, update_visual_control)
from tests.test_wp007 import actor_for, domain, point

pytestmark = pytest.mark.django_db
BACKEND = "django.contrib.auth.backends.ModelBackend"


@pytest.fixture(autouse=True)
def test_auth_backend(settings):
    settings.AUTHENTICATION_BACKENDS = [BACKEND]


def complete(actor, revision, *, measured="20", visual=QualityResult.OK):
    point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye, requirement = session.eyes.get(), session.requirements.get()
    measurement = save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal(measured))
    finish_measurement_phase(actor=actor, session=session)
    control = create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result=visual)
    complete_eye_visual_phase(actor=actor, eye=eye)
    session = finalize_inspection(actor=actor, session=session)
    return session, eye, measurement, control


def test_launcher_is_scope_filtered_and_active_only(client, domain):
    _, product, _, revision = domain
    plastic = actor_for("plastic_quality", "plastic-wp8")
    incoming = actor_for("incoming_quality", "incoming-wp8")
    point(revision)
    client.force_login(plastic, backend=BACKEND)
    response = client.get(reverse("inspections:new"))
    assert response.status_code == 200 and str(revision.id) in response.content.decode()
    client.force_login(incoming, backend=BACKEND)
    assert str(revision.id) not in client.get(reverse("inspections:new")).content.decode()
    revision.status = revision.Status.WITHDRAWN
    revision.save(update_fields=("status",))
    client.force_login(plastic, backend=BACKEND)
    assert str(revision.id) not in client.get(reverse("inspections:new")).content.decode()


def test_atomic_create_start_rejects_inactive_and_leaves_no_draft(domain):
    actor, _, _, revision = domain
    point(revision)
    revision.status = revision.Status.SUPERSEDED
    revision.save(update_fields=("status",))
    with pytest.raises(InspectionError, match="aktif"):
        create_and_start_inspection(actor=actor, drawing_revision=revision)
    assert not InspectionSession.objects.exists()


def test_atomic_create_start_rolls_back_draft_when_start_fails(domain):
    actor, _, _, revision = domain
    with pytest.raises(InspectionError, match="aktif kontrol noktası"):
        create_and_start_inspection(actor=actor, drawing_revision=revision)
    assert not InspectionSession.objects.exists()


@pytest.mark.parametrize("raw,expected", [("20", Decimal("20")), ("20.1", Decimal("20.1")), ("20,1", Decimal("20.1")), ("-0,25", Decimal("-0.25"))])
def test_factory_decimal_http_form_accepts_comma_and_dot(raw, expected):
    form = MeasurementForm({"measured_value": raw, "note": ""})
    assert form.is_valid() and form.cleaned_data["measured_value"] == expected


@pytest.mark.parametrize("raw", ["NaN", "Infinity", "1.2.3", "1,2.3", "1.000001", "1000000000"])
def test_factory_decimal_rejects_malformed_scale_and_overflow(raw):
    assert not MeasurementForm({"measured_value": raw}).is_valid()


def test_measurement_endpoint_upserts_and_rejects_cross_session(client, domain):
    actor, _, _, revision = domain
    point(revision)
    first = create_and_start_inspection(actor=actor, drawing_revision=revision)
    second = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye, requirement = first.eyes.get(), first.requirements.get()
    client.force_login(actor, backend=BACKEND)
    url = reverse("inspections:measurement-save", args=[first.id, eye.id, requirement.id])
    first_response = client.post(url, {"measured_value": "20,1", "note": "ilk"})
    second_response = client.post(url, {"measured_value": "20.0", "note": "son"})
    assert first_response.status_code == second_response.status_code == 200
    assert first_response.json()["measurement_id"] == second_response.json()["measurement_id"]
    assert Measurement.objects.count() == 1 and Measurement.objects.get().note == "son"
    cross = reverse("inspections:measurement-save", args=[second.id, eye.id, second.requirements.get().id])
    assert client.post(cross, {"measured_value": "20"}).status_code == 404


def test_measurement_rejected_for_closed_and_waiting_visual(client, domain):
    actor, _, _, revision = domain
    point(revision)
    closed = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye, requirement = closed.eyes.get(), closed.requirements.get()
    close_inspection_eye(actor=actor, eye=eye)
    client.force_login(actor, backend=BACKEND)
    url = reverse("inspections:measurement-save", args=[closed.id, eye.id, requirement.id])
    assert client.post(url, {"measured_value": "20"}).status_code == 409
    waiting = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye, requirement = waiting.eyes.get(), waiting.requirements.get()
    save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=waiting)
    url = reverse("inspections:measurement-save", args=[waiting.id, eye.id, requirement.id])
    assert client.post(url, {"measured_value": "20"}).status_code == 409


def test_finish_visual_update_and_all_result_variants(domain):
    actor, _, _, revision = domain
    point(revision)
    missing = create_and_start_inspection(actor=actor, drawing_revision=revision)
    with pytest.raises(InspectionError, match="zorunlu ölçüm eksik"):
        finish_measurement_phase(actor=actor, session=missing)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye, requirement = session.eyes.get(), session.requirements.get()
    save_measurement(actor=actor, eye=eye, requirement=requirement, measured_value=Decimal("20"))
    finish_measurement_phase(actor=actor, session=session)
    visual = create_visual_control(actor=actor, eye=eye, control_name="Yüzey", result="OK")
    update_visual_control(actor=actor, visual_control=visual, control_name="Yüzey 2", result="NOK", note="kusur")
    complete_eye_visual_phase(actor=actor, eye=eye)
    assert finalize_inspection(actor=actor, session=session).overall_result == "NOK"


def test_all_ok_and_all_closed_aggregates(domain):
    actor, _, _, revision = domain
    ok, *_ = complete(actor, revision)
    assert ok.overall_result == "OK"
    closed = create_and_start_inspection(actor=actor, drawing_revision=revision)
    close_inspection_eye(actor=actor, eye=closed.eyes.get())
    finish_measurement_phase(actor=actor, session=closed)
    assert finalize_inspection(actor=actor, session=closed).overall_result is None


def test_manager_history_only_scope_and_pagination_filter_contract(client, domain):
    actor, _, _, revision = domain
    manager = actor_for("manager", "manager-wp8")
    for number in range(27):
        InspectionSession.objects.create(drawing_revision=revision, scope="PLASTIC", operator=actor, operator_name_snapshot="Operator", lot_no=f"LOT-{number}")
    client.force_login(manager, backend=BACKEND)
    response = client.get(reverse("inspections:history"), {"q": "TR", "lot": "LOT", "status": "DRAFT", "page": 2})
    body = response.content.decode()
    assert response.status_code == 200 and "Yeni Kontrol" not in body
    assert "q=TR" in body and "lot=LOT" in body and "status=DRAFT" in body
    assert client.get(reverse("inspections:new")).status_code == 403


def test_history_scope_isolation(client, domain):
    _, _, _, plastic_revision = domain
    plastic = actor_for("plastic_quality", "plastic-history-wp8")
    incoming = actor_for("incoming_quality", "incoming-history-wp8")
    product = Product.objects.create(tr_code="INCOMING-SECRET", product_name="Incoming")
    drawing = Drawing.objects.create(product=product, scope="INCOMING_QUALITY")
    file_object = FileObject.objects.create(storage_key="objects/wp008-incoming", original_name="incoming.pdf", mime_type="application/pdf", size_bytes=1, sha256="b" * 64)
    incoming_revision = DrawingRevision.objects.create(drawing=drawing, revision_code="A", primary_file=file_object, status="ACTIVE")
    InspectionSession.objects.create(drawing_revision=plastic_revision, scope="PLASTIC", operator=plastic, operator_name_snapshot="Plastic")
    InspectionSession.objects.create(drawing_revision=incoming_revision, scope="INCOMING_QUALITY", operator=incoming, operator_name_snapshot="Incoming")
    client.force_login(plastic, backend=BACKEND)
    body = client.get(reverse("inspections:history")).content.decode()
    assert "TR — P" in body and "INCOMING-SECRET" not in body


def test_frozen_overlay_survives_definition_revision(client, domain):
    actor, _, _, revision = domain
    old = point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    author = actor_for("technical_drawing", "overlay-author")
    revise_control_point(actor=author, control_point=old.control_point, drawing_revision=revision, data={"measure_code":"99", "measure_name":"Yeni", "nominal":Decimal("21"), "lower_tolerance":Decimal(".1"), "upper_tolerance":Decimal(".1"), "unit":"mm", "page_no":2, "x_ratio":Decimal(".5"), "y_ratio":Decimal(".6"), "is_mandatory":True, "measurement_group":"Yeni", "sample_frequency":"Her", "is_critical":False, "sort_no":99, "change_reason":"test"})
    client.force_login(actor, backend=BACKEND)
    payload = client.get(reverse("inspections:overlay", args=[session.id, session.eyes.get().id])).json()["markers"][0]
    assert payload == {"requirement_id": str(session.requirements.get().id), "page_no": 1, "x_ratio": "0.250000", "y_ratio": "0.750000", "measure_code": "10", "measure_name": "Dış Çap", "is_critical": True, "state": "PENDING"}


def test_completed_correction_permissions_history_snapshots_and_aggregate(domain):
    actor, _, _, revision = domain
    session, _, measurement, _ = complete(actor, revision, measured="20.3")
    admin = actor_for("admin", "admin-wp8")
    manager = actor_for("manager", "manager-correct-wp8")
    original_actor, original_at = measurement.measured_by_id, measurement.measured_at
    snapshot_fields = [field.name for field in Measurement._meta.fields if field.name.endswith("_snapshot")]
    snapshots = {name: getattr(measurement, name) for name in snapshot_fields}
    with pytest.raises(PermissionDenied):
        correct_completed_measurement(actor=manager, measurement=measurement, new_value=Decimal("20"), reason="x")
    with pytest.raises(InspectionError, match="zorunludur"):
        correct_completed_measurement(actor=admin, measurement=measurement, new_value=Decimal("20"), reason="")
    for number, value in enumerate(("20", "20.3", "20"), 1):
        revision_row = correct_completed_measurement(actor=admin, measurement=measurement, new_value=Decimal(value), reason=f"neden {number}")
        assert revision_row.revision_no == number
    measurement.refresh_from_db(); session.refresh_from_db()
    assert measurement.result == "OK" and session.overall_result == "OK"
    assert measurement.measured_by_id == original_actor and measurement.measured_at == original_at
    assert {name: getattr(measurement, name) for name in snapshot_fields} == snapshots
    assert list(MeasurementRevision.objects.values_list("revision_no", flat=True)) == [1, 2, 3]
    assert list(MeasurementRevision.objects.values_list("old_result", "new_result")) == [("NOK", "OK"), ("OK", "NOK"), ("NOK", "OK")]
    metadata = AuditEvent.objects.filter(event_type="measurement.corrected").latest("occurred_at").metadata
    assert metadata == {"measurement_id": str(measurement.id), "revision_no": 3, "old_value": "20.3", "new_value": "20", "old_result": "NOK", "new_result": "OK", "reason": "neden 3"}


def test_visual_nok_survives_correction_and_non_completed_is_rejected(domain):
    actor, _, _, revision = domain
    session, _, measurement, _ = complete(actor, revision, measured="20.3", visual="NOK")
    admin = actor_for("admin", "admin-visual-wp8")
    correct_completed_measurement(actor=admin, measurement=measurement, new_value=Decimal("20"), reason="doğru değer")
    session.refresh_from_db()
    assert session.overall_result == "NOK"
    session.status = "WAITING_VISUAL"; session.save(update_fields=("status",))
    with pytest.raises(InspectionError):
        correct_completed_measurement(actor=admin, measurement=measurement, new_value=Decimal("20"), reason="x")


def test_static_keyboard_overlay_and_security_contract():
    root = Path(__file__).parents[1] / "apps" / "inspections" / "static" / "inspections"
    keyboard = (root / "inspection.js").read_text()
    overlay = (root / "inspection_overlay.js").read_text()
    assert "response.ok" in keyboard and "dataset.inflight" in keyboard and "finally" in keyboard
    assert "inspection:focus-row" in keyboard and "inspection:marker-state" in keyboard
    assert "data.result" in keyboard and "lower_limit" not in keyboard and "upper_limit" not in keyboard
    assert "inspectionOverlayUrl" in overlay and "requirement_id" in overlay
    assert "http://" not in keyboard + overlay and "https://" not in keyboard + overlay and "/data/drawings" not in keyboard + overlay
    assert (root / "inspection.css").is_file()


def test_overlay_css_is_isolated_and_loaded_only_in_overlay_viewer(client, domain):
    actor, _, _, revision = domain
    point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    eye = session.eyes.get()
    client.force_login(actor, backend=BACKEND)
    normal = client.get(reverse("drawings:revision-viewer", args=[revision.id])).content.decode()
    overlay = client.get(reverse("drawings:revision-viewer", args=[revision.id]), {"inspection": session.id, "eye": eye.id}).content.decode()
    assert "inspections/inspection_overlay.css" not in normal
    assert "inspections/inspection.css" not in normal
    assert "inspections/inspection_overlay.css" in overlay
    assert "inspections/inspection.css" not in overlay
    overlay_css = (Path(__file__).parents[1] / "apps/inspections/static/inspections/inspection_overlay.css").read_text()
    assert ".inspection-marker" in overlay_css
    assert "body{" not in overlay_css and "main{" not in overlay_css and "nav{" not in overlay_css


def test_static_cross_page_highlight_and_live_progress_contract():
    root = Path(__file__).parents[1] / "apps/inspections/static/inspections"
    overlay = (root / "inspection_overlay.js").read_text()
    keyboard = (root / "inspection.js").read_text()
    assert "marker.page_no" in overlay and "pageInput.dispatchEvent" in overlay
    assert "drawingviewer:rendered" in overlay and "focusPendingMarker" in overlay
    assert "data-mandatory=\"true\"" in keyboard
    assert "data-mandatory-done" in keyboard and "data.result" in keyboard


def test_manager_active_session_link_is_read_only_detail(client, domain):
    actor, _, _, revision = domain
    point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    manager = actor_for("manager", "manager-active-wp8")
    client.force_login(manager, backend=BACKEND)
    body = client.get(reverse("inspections:history")).content.decode()
    assert reverse("inspections:detail", args=[session.id]) in body
    assert reverse("inspections:work", args=[session.id]) not in body


def test_malformed_workspace_and_viewer_queries_never_500(client, domain):
    actor, _, _, revision = domain
    point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    client.force_login(actor, backend=BACKEND)
    assert client.get(reverse("inspections:work", args=[session.id]), {"eye": "not-a-uuid"}).status_code == 400
    viewer = reverse("drawings:revision-viewer", args=[revision.id])
    assert client.get(viewer, {"inspection": "not-a-uuid", "eye": "also-bad"}).status_code == 400
    assert client.get(viewer, {"inspection": session.id, "eye": "also-bad"}).status_code == 400


def test_invalid_history_filters_render_safely(client, domain):
    actor, _, _, _ = domain
    manager = actor_for("manager", "manager-invalid-filter-wp8")
    client.force_login(manager, backend=BACKEND)
    response = client.get(reverse("inspections:history"), {"date_from": "not-a-date", "date_to": "2026-99-99", "status": "UNKNOWN", "result": "MAYBE"})
    assert response.status_code == 200
    assert "Geçersiz filtreler yok sayıldı" in response.content.decode()


def test_inactive_locked_drawing_rejects_start_without_orphan(domain):
    actor, _, drawing, revision = domain
    point(revision)
    drawing.is_active = False
    drawing.save(update_fields=("is_active",))
    with pytest.raises(InspectionError, match="Pasif teknik resim"):
        create_and_start_inspection(actor=actor, drawing_revision=revision)
    assert not InspectionSession.objects.exists()


def test_overlay_endpoint_rejects_unauthorized_scope(client, domain):
    actor, _, _, revision = domain
    point(revision)
    session = create_and_start_inspection(actor=actor, drawing_revision=revision)
    incoming = actor_for("incoming_quality", "incoming-overlay-denied-wp8")
    client.force_login(incoming, backend=BACKEND)
    response = client.get(reverse("inspections:overlay", args=[session.id, session.eyes.get().id]))
    assert response.status_code == 403
