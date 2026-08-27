from decimal import Decimal
from pathlib import Path
import uuid
import pytest
from django.core.exceptions import PermissionDenied
from django.db import IntegrityError, transaction
from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.control_points.forms import parse_factory_decimal
from apps.control_points.models import ControlPoint, ControlPointVersion
from apps.control_points.selectors import list_active_versions_for_revision
from apps.control_points.services import (
    ControlPointError,
    copy_control_points_to_revision,
    create_control_point,
    deactivate_control_point,
    revise_control_point,
)
from apps.core.models import FileObject
from apps.drawings.models import Drawing, DrawingRevision
from apps.products.models import Product

pytestmark = pytest.mark.django_db
WEB_ROOT = Path(__file__).resolve().parents[1]


@pytest.fixture
def domain():
    seed_authorization_baseline()
    manager = User.objects.create_user(username="technical")
    assignment = UserRole.objects.create(
        user=manager, role=Role.objects.get(code="technical_drawing")
    )
    seed_assignment_scopes(assignment)
    product = Product.objects.create(tr_code="TR", product_name="P")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    file = FileObject.objects.create(
        storage_key="objects/aa/bb/wp006",
        original_name="x.pdf",
        mime_type="application/pdf",
        size_bytes=1,
        sha256="a" * 64,
    )
    a = DrawingRevision.objects.create(
        drawing=drawing, revision_code="A", primary_file=file
    )
    b = DrawingRevision.objects.create(
        drawing=drawing, revision_code="B", primary_file=file
    )
    return manager, drawing, a, b


def values(**changes):
    data = {
        "measure_code": "10",
        "measure_name": "Çap",
        "nominal": Decimal("20"),
        "lower_tolerance": Decimal("0.1"),
        "upper_tolerance": Decimal("0.2"),
        "unit": "mm",
        "page_no": 1,
        "x_ratio": Decimal("0.250000"),
        "y_ratio": Decimal("0.750000"),
        "is_mandatory": True,
        "measurement_group": "Genel",
        "sample_frequency": "Her Kontrol",
        "is_critical": False,
        "sort_no": 10,
        "change_reason": "ilk",
    }
    data.update(changes)
    return data


@pytest.mark.parametrize(
    "raw,expected",
    [("12,5", Decimal("12.5")), ("12.5", Decimal("12.5")), ("-0,10", Decimal("-0.10"))],
)
def test_controlled_decimal_parser(raw, expected):
    assert parse_factory_decimal(raw) == expected


@pytest.mark.parametrize("raw", ["1,2.3", "abc", "1.123456", "NaN", ""])
def test_decimal_parser_rejects_malformed_or_excess_precision(raw):
    with pytest.raises(Exception):
        parse_factory_decimal(raw)


def test_create_revise_deactivate_history_and_decimal_limits(domain):
    actor, _, revision, _ = domain
    first = create_control_point(actor=actor, drawing_revision=revision, data=values())
    assert isinstance(first.id, uuid.UUID) and isinstance(first.nominal, Decimal)
    assert (
        first.lower_tolerance,
        first.upper_tolerance,
        first.lower_limit,
        first.upper_limit,
    ) == (Decimal("-0.1"), Decimal("0.2"), Decimal("19.9"), Decimal("20.2"))
    point = first.control_point
    stable = point.spc_key
    second = revise_control_point(
        actor=actor,
        control_point=point,
        drawing_revision=revision,
        data=values(
            measure_code="11",
            measure_name="Dış Çap",
            x_ratio=Decimal("0.5"),
            change_reason="konum",
        ),
    )
    first.refresh_from_db()
    assert (
        not first.is_active
        and first.valid_to
        and second.version_no == 2
        and point.spc_key == stable
    )
    third = revise_control_point(
        actor=actor,
        control_point=point,
        drawing_revision=revision,
        data=values(measure_code="12", change_reason="üç"),
    )
    assert (
        third.version_no == 3
        and point.versions.count() == 3
        and third.change_reason == "üç"
    )
    deactivate_control_point(
        actor=actor, control_point=point, drawing_revision=revision
    )
    third.refresh_from_db()
    assert not third.is_active and point.versions.count() == 3
    assert AuditEvent.objects.filter(
        event_type="control_point.version_deactivated"
    ).exists()


def test_copy_reuses_logical_identity_and_leaves_source(domain):
    actor, _, source, target = domain
    originals = [
        create_control_point(
            actor=actor,
            drawing_revision=source,
            data=values(measure_code=str(i), sort_no=i),
        )
        for i in (1, 2)
    ]
    copied = copy_control_points_to_revision(actor, source, target)
    assert {v.control_point_id for v in copied} == {
        v.control_point_id for v in originals
    }
    assert all(v.version_no == 2 for v in copied)
    assert all(v.is_active for v in originals) and all(
        v.drawing_revision_id == target.id for v in copied
    )
    assert (
        AuditEvent.objects.filter(
            event_type="control_points.copied_to_revision"
        ).count()
        == 1
    )
    with pytest.raises(ControlPointError):
        copy_control_points_to_revision(actor, source, target)


def test_authorization_and_revision_mutability(domain):
    actor, _, revision, _ = domain
    reader = User.objects.create_user(username="reader")
    assignment = UserRole.objects.create(
        user=reader, role=Role.objects.get(code="manager")
    )
    seed_assignment_scopes(assignment)
    with pytest.raises(PermissionDenied):
        create_control_point(actor=reader, drawing_revision=revision, data=values())
    point = create_control_point(actor=actor, drawing_revision=revision, data=values())
    assert list(list_active_versions_for_revision(reader, revision)) == [point]
    revision.status = "SUPERSEDED"
    revision.save(update_fields=("status",))
    with pytest.raises(ControlPointError):
        revise_control_point(
            actor=actor,
            control_point=point.control_point,
            drawing_revision=revision,
            data=values(),
        )
    assert list(list_active_versions_for_revision(reader, revision)) == [point]


def test_structural_uniqueness_but_not_speculative_business_uniqueness(domain):
    actor, drawing, a, b = domain
    first = create_control_point(actor=actor, drawing_revision=a, data=values())
    # Same SPC and measure code are valid hard-schema inputs; UUID is identity.
    second = ControlPoint.objects.create(
        drawing=drawing, spc_key=first.control_point.spc_key
    )
    ControlPointVersion.objects.create(
        control_point=second,
        drawing_revision=a,
        version_no=1,
        measure_code=first.measure_code,
        measure_name="other",
        nominal=20,
        lower_tolerance=-1,
        upper_tolerance=1,
        lower_limit=19,
        upper_limit=21,
        page_no=1,
        x_ratio=0,
        y_ratio=1,
    )
    ControlPointVersion.objects.create(
        control_point=first.control_point,
        drawing_revision=b,
        version_no=2,
        measure_code="10",
        measure_name="copy",
        nominal=20,
        lower_tolerance=-1,
        upper_tolerance=1,
        lower_limit=19,
        upper_limit=21,
        page_no=1,
        x_ratio=0,
        y_ratio=1,
    )
    with pytest.raises(IntegrityError), transaction.atomic():
        ControlPointVersion.objects.create(
            control_point=first.control_point,
            drawing_revision=a,
            version_no=1,
            measure_code="x",
            measure_name="x",
            nominal=1,
            lower_tolerance=0,
            upper_tolerance=0,
            lower_limit=1,
            upper_limit=1,
            page_no=1,
            x_ratio=0,
            y_ratio=0,
        )


def test_overlay_contract_uses_css_normalized_coordinates_only():
    js = (
        WEB_ROOT
        / "apps"
        / "control_points"
        / "static"
        / "control_points"
        / "control_points.js"
    ).read_text(encoding="utf-8")
    assert "drawingviewer:rendered" in js and "getBoundingClientRect" in js
    assert "clientX - rect.left" in js and "clientY - rect.top" in js
    assert "devicePixelRatio" not in js and "canvas.width" not in js
