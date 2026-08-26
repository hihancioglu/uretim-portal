import uuid
from pathlib import Path

import pytest
from django.contrib.auth import get_user_model
from django.core.exceptions import PermissionDenied, ValidationError

from apps.accounts.models import Role, UserRole
from apps.accounts.seeding import seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.products.models import Mold, Product, ProductMold
from apps.products.selectors import list_products
from apps.products.services import (
    create_mold,
    create_product,
    deactivate_mold,
    deactivate_product,
    deactivate_product_mold_link,
    link_product_mold,
    update_mold,
    update_product,
)

pytestmark = pytest.mark.django_db


def make_user(name, role_code="admin", *, active=True, assignment_active=True):
    seed_authorization_baseline()
    account = get_user_model().objects.create_user(username=name, is_active=active)
    UserRole.objects.create(
        user=account,
        role=Role.objects.get(code=role_code),
        is_active=assignment_active,
    )
    return account


def test_product_and_mold_use_uuid_and_preserve_business_identifiers():
    actor = make_user("creator")
    product = create_product(actor=actor, tr_code=" 00-tr/A ", product_name="Ürün")
    mold = create_mold(actor=actor, mold_code=" 01-m/B ", cavity_count=2)

    assert isinstance(product.id, uuid.UUID)
    assert isinstance(mold.id, uuid.UUID)
    assert product.tr_code == " 00-tr/A "
    assert mold.mold_code == " 01-m/B "


def test_many_to_many_supports_both_directions_and_duplicate_links():
    actor = make_user("linker")
    first_product = create_product(actor=actor, tr_code="TR", product_name="One")
    second_product = create_product(actor=actor, tr_code="TR-2", product_name="Two")
    first_mold = create_mold(actor=actor, mold_code="M")
    second_mold = create_mold(actor=actor, mold_code="M-2")

    link_product_mold(actor=actor, product=first_product, mold=first_mold)
    link_product_mold(actor=actor, product=first_product, mold=second_mold)
    link_product_mold(actor=actor, product=second_product, mold=first_mold)
    link_product_mold(actor=actor, product=first_product, mold=first_mold)

    assert first_product.molds.count() == 3
    assert first_mold.products.count() == 3


def test_business_codes_are_intentionally_not_unique():
    actor = make_user("duplicates")
    create_product(actor=actor, tr_code="same", product_name="One")
    create_product(actor=actor, tr_code="same", product_name="Two")
    create_mold(actor=actor, mold_code="same")
    create_mold(actor=actor, mold_code="same")

    assert Product.objects.filter(tr_code="same").count() == 2
    assert Mold.objects.filter(mold_code="same").count() == 2


@pytest.mark.parametrize("value", [0, -1, 1.5, True, "2"])
def test_cavity_count_rejects_non_positive_or_non_integer_values(value):
    actor = make_user(f"cavity-{value}")
    with pytest.raises(ValidationError):
        create_mold(actor=actor, mold_code="M", cavity_count=value)


@pytest.mark.parametrize(
    ("service", "kwargs"),
    [
        (create_product, {"tr_code": " ", "product_name": "Product"}),
        (create_product, {"tr_code": "TR", "product_name": "\t"}),
        (create_mold, {"mold_code": "\n"}),
    ],
)
def test_blank_required_values_are_rejected(service, kwargs):
    with pytest.raises(ValidationError):
        service(actor=make_user(f"blank-{service.__name__}-{len(kwargs)}"), **kwargs)


def test_deactivation_does_not_physically_delete_entities_or_link():
    actor = make_user("deactivator")
    product = create_product(actor=actor, tr_code="TR", product_name="Product")
    mold = create_mold(actor=actor, mold_code="M")
    link = link_product_mold(actor=actor, product=product, mold=mold)

    deactivate_product(actor=actor, product=product)
    deactivate_mold(actor=actor, mold=mold)
    deactivate_product_mold_link(actor=actor, link=link)

    assert not Product.objects.get(pk=product.pk).is_active
    assert not Mold.objects.get(pk=mold.pk).is_active
    assert not ProductMold.objects.get(pk=link.pk).is_active


@pytest.mark.parametrize(
    "account",
    [
        pytest.param(None, id="anonymous"),
        pytest.param("inactive-user", id="inactive-user"),
        pytest.param("inactive-role", id="inactive-role"),
        pytest.param("unauthorized-role", id="unauthorized-role"),
    ],
)
def test_mutations_require_active_authorized_actor(account):
    if account is None:
        actor = None
    elif account == "inactive-user":
        actor = make_user(account, active=False)
    elif account == "inactive-role":
        actor = make_user(account, assignment_active=False)
    else:
        actor = make_user(account, role_code="manager")

    with pytest.raises(PermissionDenied):
        create_product(actor=actor, tr_code="TR", product_name="Product")
    assert not Product.objects.exists()
    assert not AuditEvent.objects.filter(event_type="product.created").exists()


def test_read_selector_requires_drawings_view():
    with pytest.raises(PermissionDenied):
        list_products(actor=make_user("no-read", role_code="planning"))
    assert list(list_products(actor=make_user("reader", role_code="manager"))) == []


def test_all_successful_mutations_create_safe_audits_with_update_snapshots():
    actor = make_user("auditor")
    product = create_product(actor=actor, tr_code="TR", product_name="Before")
    product = update_product(actor=actor, product=product, product_name="After")
    mold = create_mold(actor=actor, mold_code="M")
    mold = update_mold(actor=actor, mold=mold, cavity_count=4)
    link = link_product_mold(actor=actor, product=product, mold=mold)
    deactivate_product_mold_link(actor=actor, link=link)
    deactivate_product(actor=actor, product=product)
    deactivate_mold(actor=actor, mold=mold)

    expected = {
        "product.created",
        "product.updated",
        "product.deactivated",
        "mold.created",
        "mold.updated",
        "mold.deactivated",
        "product_mold.linked",
        "product_mold.deactivated",
    }
    assert (
        set(AuditEvent.objects.filter(actor=actor).values_list("event_type", flat=True))
        == expected
    )
    update_event = AuditEvent.objects.get(event_type="product.updated")
    assert update_event.metadata["before"]["product_name"] == "Before"
    assert update_event.metadata["after"]["product_name"] == "After"


def test_wp003_contains_no_legacy_writer_or_assumed_uniqueness():
    app_root = Path(__file__).parents[1] / "apps/products"
    source = "\n".join(
        path.read_text(encoding="utf-8") for path in app_root.rglob("*.py")
    )
    assert "legacy/" not in source
    assert "UniqueConstraint" not in source
    assert "unique=True" not in source
