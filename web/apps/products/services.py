from django.core.exceptions import ValidationError
from django.db import transaction

from apps.accounts.authz import require_action
from apps.audit.services import create_audit_event

from .models import Mold, Product, ProductMold

MANAGE_PERMISSION = "drawings.manage"
_UNSET = object()


def _require_text(value, field):
    if not isinstance(value, str) or not value.strip():
        raise ValidationError({field: "This field is required."})


def _validate_cavity_count(value):
    if value is not None and (
        isinstance(value, bool) or not isinstance(value, int) or value <= 0
    ):
        raise ValidationError(
            {"cavity_count": "Cavity count must be a positive integer or null."}
        )


def _actor_snapshot(actor):
    return actor.get_username() if actor is not None else ""


def _audit(*, actor, event_type, entity, metadata=None):
    create_audit_event(
        actor=actor,
        actor_snapshot=_actor_snapshot(actor),
        event_type=event_type,
        entity_type=entity._meta.label_lower,
        entity_id=str(entity.id),
        metadata=metadata or {},
    )


def _product_snapshot(product):
    return {
        "tr_code": product.tr_code,
        "product_name": product.product_name,
        "plastic_code": product.plastic_code,
        "material": product.material,
        "color_name": product.color_name,
        "is_active": product.is_active,
    }


def _mold_snapshot(mold):
    return {
        "mold_code": mold.mold_code,
        "description": mold.description,
        "cavity_count": mold.cavity_count,
        "is_active": mold.is_active,
    }


@transaction.atomic
def create_product(
    *, actor, tr_code, product_name, plastic_code="", material="", color_name=""
):
    require_action(actor, MANAGE_PERMISSION)
    _require_text(tr_code, "tr_code")
    _require_text(product_name, "product_name")
    product = Product.objects.create(
        tr_code=tr_code,
        product_name=product_name,
        plastic_code=plastic_code,
        material=material,
        color_name=color_name,
        created_by=actor,
        updated_by=actor,
    )
    _audit(
        actor=actor,
        event_type="product.created",
        entity=product,
        metadata={"after": _product_snapshot(product)},
    )
    return product


@transaction.atomic
def update_product(
    *,
    actor,
    product,
    tr_code=_UNSET,
    product_name=_UNSET,
    plastic_code=_UNSET,
    material=_UNSET,
    color_name=_UNSET,
):
    require_action(actor, MANAGE_PERMISSION)
    product = Product.objects.select_for_update().get(pk=product.pk)
    before = _product_snapshot(product)
    values = {
        "tr_code": tr_code,
        "product_name": product_name,
        "plastic_code": plastic_code,
        "material": material,
        "color_name": color_name,
    }
    for field, value in values.items():
        if value is not _UNSET:
            if field in {"tr_code", "product_name"}:
                _require_text(value, field)
            setattr(product, field, value)
    product.updated_by = actor
    product.save()
    _audit(
        actor=actor,
        event_type="product.updated",
        entity=product,
        metadata={"before": before, "after": _product_snapshot(product)},
    )
    return product


@transaction.atomic
def deactivate_product(*, actor, product):
    require_action(actor, MANAGE_PERMISSION)
    product = Product.objects.select_for_update().get(pk=product.pk)
    product.is_active = False
    product.updated_by = actor
    product.save(update_fields=("is_active", "updated_by", "updated_at"))
    _audit(actor=actor, event_type="product.deactivated", entity=product)
    return product


@transaction.atomic
def create_mold(*, actor, mold_code, description="", cavity_count=None):
    require_action(actor, MANAGE_PERMISSION)
    _require_text(mold_code, "mold_code")
    _validate_cavity_count(cavity_count)
    mold = Mold.objects.create(
        mold_code=mold_code,
        description=description,
        cavity_count=cavity_count,
        created_by=actor,
        updated_by=actor,
    )
    _audit(
        actor=actor,
        event_type="mold.created",
        entity=mold,
        metadata={"after": _mold_snapshot(mold)},
    )
    return mold


@transaction.atomic
def update_mold(
    *, actor, mold, mold_code=_UNSET, description=_UNSET, cavity_count=_UNSET
):
    require_action(actor, MANAGE_PERMISSION)
    mold = Mold.objects.select_for_update().get(pk=mold.pk)
    before = _mold_snapshot(mold)
    if mold_code is not _UNSET:
        _require_text(mold_code, "mold_code")
        mold.mold_code = mold_code
    if description is not _UNSET:
        mold.description = description
    if cavity_count is not _UNSET:
        _validate_cavity_count(cavity_count)
        mold.cavity_count = cavity_count
    mold.updated_by = actor
    mold.save()
    _audit(
        actor=actor,
        event_type="mold.updated",
        entity=mold,
        metadata={"before": before, "after": _mold_snapshot(mold)},
    )
    return mold


@transaction.atomic
def deactivate_mold(*, actor, mold):
    require_action(actor, MANAGE_PERMISSION)
    mold = Mold.objects.select_for_update().get(pk=mold.pk)
    mold.is_active = False
    mold.updated_by = actor
    mold.save(update_fields=("is_active", "updated_by", "updated_at"))
    _audit(actor=actor, event_type="mold.deactivated", entity=mold)
    return mold


@transaction.atomic
def link_product_mold(*, actor, product, mold):
    require_action(actor, MANAGE_PERMISSION)
    link = ProductMold.objects.create(
        product=product, mold=mold, created_by=actor, updated_by=actor
    )
    _audit(
        actor=actor,
        event_type="product_mold.linked",
        entity=link,
        metadata={"product_id": str(product.id), "mold_id": str(mold.id)},
    )
    return link


@transaction.atomic
def deactivate_product_mold_link(*, actor, link):
    require_action(actor, MANAGE_PERMISSION)
    link = ProductMold.objects.select_for_update().get(pk=link.pk)
    link.is_active = False
    link.updated_by = actor
    link.save(update_fields=("is_active", "updated_by", "updated_at"))
    _audit(
        actor=actor,
        event_type="product_mold.deactivated",
        entity=link,
        metadata={"product_id": str(link.product_id), "mold_id": str(link.mold_id)},
    )
    return link
