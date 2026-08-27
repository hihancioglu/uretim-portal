from django.db.models import Count, OuterRef, Subquery

from apps.accounts.authz import require_action
from apps.products.selectors import list_products, search_products

from .models import Drawing, DrawingRevision


def list_products_for_management(*, actor, query=""):
    require_action(actor, "drawings.manage")
    products = (
        search_products(actor=actor, query=query, active_only=False)
        if query
        else list_products(actor=actor, active_only=False)
    )
    return products.annotate(drawing_count=Count("drawings", distinct=True)).order_by(
        "tr_code", "product_name", "id"
    )


def list_drawings_for_product(*, actor, product):
    require_action(actor, "drawings.manage")
    active_revision = DrawingRevision.objects.filter(
        drawing_id=OuterRef("pk"), status=DrawingRevision.Status.ACTIVE
    ).values("revision_code")[:1]
    latest_revision = DrawingRevision.objects.filter(
        drawing_id=OuterRef("pk")
    ).order_by("-created_at", "-id")
    return (
        Drawing.objects.filter(product=product)
        .select_related("product")
        .annotate(
            active_revision_code=Subquery(active_revision),
            latest_revision_code=Subquery(latest_revision.values("revision_code")[:1]),
        )
        .order_by("scope", "title", "id")
    )


def get_drawing_for_management(*, actor, drawing_id):
    require_action(actor, "drawings.manage")
    return Drawing.objects.select_related("product").get(pk=drawing_id)


def list_revisions_for_drawing(*, actor, drawing):
    require_action(actor, "drawings.manage")
    return drawing.revisions.select_related(
        "primary_file", "created_by", "approved_by"
    ).order_by("-created_at", "-id")
