import io
import warnings

import pytest
from django.core.files.uploadedfile import SimpleUploadedFile
from django.urls import reverse

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.core.models import FileObject
from apps.core.storage import drawing_storage
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


def test_product_pagination_is_stable_and_ordered_without_warning(console):
    client, _ = console
    Product.objects.bulk_create(
        [
            Product(tr_code=f"TR-{index // 3:03}", product_name=f"Ürün {index % 3}")
            for index in range(60)
        ]
    )
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        first = client.get(reverse("drawings:manage-home"), {"page": 1})
        second = client.get(reverse("drawings:manage-home"), {"page": 2})
    first_ids = [product.id for product in first.context["page"].object_list]
    second_ids = [product.id for product in second.context["page"].object_list]
    expected = list(
        Product.objects.order_by("tr_code", "product_name", "id").values_list(
            "id", flat=True
        )[:50]
    )
    assert first_ids + second_ids == expected
    assert set(first_ids).isdisjoint(second_ids)
    assert not any(
        item.category.__name__ == "UnorderedObjectListWarning" for item in caught
    )


def _upload_revision(client, drawing, *, code="A", name="example.pdf", body=PDF):
    response = client.post(
        reverse("drawings:manage-revision-create", args=[drawing.id]),
        {
            "revision_code": code,
            "change_reason": "ilk",
            "drawing_file": SimpleUploadedFile(name, body),
        },
    )
    assert response.status_code == 302
    return DrawingRevision.objects.get(drawing=drawing, revision_code=code)


@pytest.mark.parametrize(
    ("name", "expected"),
    [
        ("example.pdf.enc", FileObject.EncryptionScheme.LEGACY_AES_GCM),
        ("EXAMPLE.PDF.ENC", FileObject.EncryptionScheme.LEGACY_AES_GCM),
        ("example.pdf", FileObject.EncryptionScheme.NONE),
    ],
)
def test_management_upload_classifies_encryption_scheme(console, name, expected):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    revision = _upload_revision(client, drawing, name=name)
    assert revision.primary_file.encryption_scheme == expected


def test_draft_metadata_edit_and_file_replacement_preserve_old_file(console):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    revision = _upload_revision(client, drawing)
    old_file = revision.primary_file
    edit = client.post(
        reverse("drawings:manage-revision-edit", args=[revision.id]),
        {"revision_code": "B", "change_reason": "pilot düzeltmesi"},
    )
    assert edit.status_code == 302
    revision.refresh_from_db()
    assert (revision.revision_code, revision.change_reason) == (
        "B",
        "pilot düzeltmesi",
    )
    new_bytes = b"encrypted historical bytes"
    replace = client.post(
        reverse("drawings:manage-revision-replace-file", args=[revision.id]),
        {"drawing_file": SimpleUploadedFile("replacement.pdf.enc", new_bytes)},
    )
    assert replace.status_code == 302
    revision.refresh_from_db()
    assert revision.primary_file_id != old_file.id
    assert FileObject.objects.filter(pk=old_file.id).exists()
    assert (
        revision.primary_file.encryption_scheme
        == FileObject.EncryptionScheme.LEGACY_AES_GCM
    )
    with drawing_storage().open(revision.primary_file.storage_key) as stream:
        assert stream.read() == new_bytes


@pytest.mark.parametrize("status", ["ACTIVE", "SUPERSEDED", "WITHDRAWN"])
def test_non_draft_edit_and_replacement_are_denied(console, status):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    revision = _upload_revision(client, drawing)
    original_file_id = revision.primary_file_id
    revision.status = status
    revision.save(update_fields=["status"])
    edit = client.post(
        reverse("drawings:manage-revision-edit", args=[revision.id]),
        {"revision_code": "illegal", "change_reason": "illegal"},
    )
    replace = client.post(
        reverse("drawings:manage-revision-replace-file", args=[revision.id]),
        {"drawing_file": SimpleUploadedFile("illegal.pdf", PDF + b"changed")},
    )
    revision.refresh_from_db()
    assert edit.status_code == 403 and replace.status_code == 403
    assert revision.revision_code == "A"
    assert revision.primary_file_id == original_file_id


def test_withdraw_active_revision_keeps_file_and_activates_no_history(console):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    revision = _upload_revision(client, drawing)
    file_id = revision.primary_file_id
    client.post(reverse("drawings:manage-revision-activate", args=[revision.id]))
    response = client.post(
        reverse("drawings:manage-revision-withdraw", args=[revision.id])
    )
    revision.refresh_from_db()
    assert response.status_code == 302 and revision.status == "WITHDRAWN"
    assert FileObject.objects.filter(pk=file_id).exists()
    assert not DrawingRevision.objects.filter(
        drawing=drawing, status=DrawingRevision.Status.ACTIVE
    ).exists()


def test_invalid_drawing_scope_is_safe_form_error(console):
    client, _ = console
    product = Product.objects.create(tr_code="TR", product_name="Pilot")
    response = client.post(
        reverse("drawings:manage-drawing-create", args=[product.id]),
        {"scope": "UNKNOWN", "title": "Geçersiz"},
    )
    assert response.status_code == 200
    assert response.context["form"].errors["scope"]
    assert not Drawing.objects.filter(product=product).exists()
