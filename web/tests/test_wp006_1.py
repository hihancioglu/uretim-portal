import io

import pytest
from django.urls import reverse

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.drawings.models import Drawing, DrawingRevision
from apps.products.models import Product

pytestmark = pytest.mark.django_db
BACKEND = "django.contrib.auth.backends.ModelBackend"
PDF = b"%PDF-1.7\n1 0 obj<</Type/Catalog>>endobj\n%%EOF"


def user_with_role(code, username):
    user = User.objects.create_user(username=username)
    assignment = UserRole.objects.create(user=user, role=Role.objects.get(code=code))
    seed_assignment_scopes(assignment)
    return user


@pytest.fixture
def console(client, settings, tmp_path):
    settings.AUTHENTICATION_BACKENDS = [BACKEND]
    settings.DRAWING_STORAGE_ROOT = str(tmp_path)
    seed_authorization_baseline()
    actor = user_with_role("technical_drawing", "technical-console")
    client.force_login(actor, backend=BACKEND)
    return client, actor


def test_management_authorization_matrix(client, settings):
    settings.AUTHENTICATION_BACKENDS = [BACKEND]
    seed_authorization_baseline()
    url = reverse("drawings:manage-home")
    assert client.get(url).status_code == 403
    for role_code, allowed in (
        (None, False),
        ("manager", False),
        ("quality_manager", False),
        ("technical_drawing", True),
        ("admin", True),
    ):
        user = (
            user_with_role(role_code, f"user-{role_code}")
            if role_code
            else User.objects.create_user(username="no-role")
        )
        client.force_login(user, backend=BACKEND)
        assert (client.get(url).status_code == 200) is allowed


def test_product_search_create_edit_and_deactivate_preserves_drawing(console):
    client, _ = console
    create = client.post(
        reverse("drawings:manage-product-create"),
        {
            "tr_code": "TR-TEST-001",
            "product_name": "Pilot Sayaç",
            "plastic_code": "PL-001",
            "material": "ABS",
            "color_name": "Siyah",
        },
    )
    product = Product.objects.get(tr_code="TR-TEST-001")
    assert create.status_code == 302
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    for query in ("TR-TEST", "Pilot Sayaç", "PL-001"):
        assert (
            product.tr_code
            in client.get(
                reverse("drawings:manage-home"), {"q": query}
            ).content.decode()
        )
    client.post(
        reverse("drawings:manage-product-edit", args=[product.id]),
        {
            "tr_code": product.tr_code,
            "product_name": "Pilot Sayaç 2",
            "plastic_code": "PL-001",
            "material": "ABS",
            "color_name": "Siyah",
        },
    )
    client.post(reverse("drawings:manage-product-deactivate", args=[product.id]))
    product.refresh_from_db()
    assert product.product_name == "Pilot Sayaç 2" and not product.is_active
    assert Drawing.objects.filter(pk=drawing.id).exists()
    assert AuditEvent.objects.filter(event_type="product.deactivated").exists()


def test_pilot_drawing_revision_lifecycle_and_private_links(console, settings):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    response = client.post(
        reverse("drawings:manage-drawing-create", args=[product.id]),
        {"scope": "PLASTIC", "title": "Gövde Teknik Resmi"},
    )
    drawing = Drawing.objects.get(product=product)
    assert response.status_code == 302

    # Django's test upload requires a named file-like object.
    for code in ("A", "B"):
        file = io.BytesIO(PDF)
        file.name = f"revision-{code}.pdf"
        response = client.post(
            reverse("drawings:manage-revision-create", args=[drawing.id]),
            {"revision_code": code, "change_reason": "yayın", "drawing_file": file},
        )
        assert response.status_code == 302
        revision = DrawingRevision.objects.get(drawing=drawing, revision_code=code)
        assert revision.primary_file.size_bytes == len(PDF)
        client.post(reverse("drawings:manage-revision-activate", args=[revision.id]))
    a = DrawingRevision.objects.get(drawing=drawing, revision_code="A")
    b = DrawingRevision.objects.get(drawing=drawing, revision_code="B")
    assert a.status == "SUPERSEDED" and b.status == "ACTIVE"
    html = client.get(
        reverse("drawings:manage-drawing-detail", args=[drawing.id])
    ).content.decode()
    assert reverse("drawings:revision-viewer", args=[b.id]) in html
    assert b.primary_file.storage_key not in html
    assert settings.DRAWING_STORAGE_ROOT not in html
