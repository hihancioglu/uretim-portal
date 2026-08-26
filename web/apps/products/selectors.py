from django.db.models import Q

from apps.accounts.authz import require_action

from .models import Mold, Product, ProductMold

VIEW_PERMISSION = "drawings.view"


def list_products(*, actor, active_only=True):
    require_action(actor, VIEW_PERMISSION)
    queryset = Product.objects.all()
    return queryset.filter(is_active=True) if active_only else queryset


def get_product(*, actor, product_id, active_only=False):
    return list_products(actor=actor, active_only=active_only).get(id=product_id)


def search_products(*, actor, query, active_only=True):
    return list_products(actor=actor, active_only=active_only).filter(
        Q(tr_code__icontains=query)
        | Q(product_name__icontains=query)
        | Q(plastic_code__icontains=query)
    )


def list_molds(*, actor, active_only=True):
    require_action(actor, VIEW_PERMISSION)
    queryset = Mold.objects.all()
    return queryset.filter(is_active=True) if active_only else queryset


def get_mold(*, actor, mold_id, active_only=False):
    return list_molds(actor=actor, active_only=active_only).get(id=mold_id)


def search_molds(*, actor, query, active_only=True):
    return list_molds(actor=actor, active_only=active_only).filter(
        Q(mold_code__icontains=query) | Q(description__icontains=query)
    )


def list_product_molds(*, actor, product=None, mold=None, active_only=True):
    require_action(actor, VIEW_PERMISSION)
    queryset = ProductMold.objects.select_related("product", "mold")
    if active_only:
        queryset = queryset.filter(is_active=True)
    if product is not None:
        queryset = queryset.filter(product=product)
    if mold is not None:
        queryset = queryset.filter(mold=mold)
    return queryset
