import io
from pathlib import Path

import pytest
from django.urls import reverse

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.core.models import FileObject
from apps.core.storage.filesystem import FilesystemStorage
from apps.drawings.models import Drawing, DrawingRevision
from apps.products.models import Product

pytestmark = pytest.mark.django_db
TEST_BACKEND = "django.contrib.auth.backends.ModelBackend"
PDF = b"%PDF-1.7\n0123456789abcdefghij\n%%EOF"


@pytest.fixture
def drawing_data(tmp_path, settings):
    settings.DRAWING_STORAGE_ROOT = str(tmp_path)
    settings.AUTHENTICATION_BACKENDS = [TEST_BACKEND]
    seed_authorization_baseline()
    owner = User.objects.create_user(username="owner")
    product = Product.objects.create(tr_code="T", product_name="P")
    drawing = Drawing.objects.create(product=product, scope="PLASTIC")
    storage = FilesystemStorage(tmp_path, 10_000)
    stored = storage.store(io.BytesIO(PDF), "drawing.pdf")
    file_object = FileObject.objects.create(
        storage_key=stored.storage_key,
        original_name="drawing.pdf",
        mime_type="client/value",
        size_bytes=stored.size_bytes,
        sha256=stored.sha256,
        created_by=owner,
    )
    revision = DrawingRevision.objects.create(
        drawing=drawing, revision_code="A", primary_file=file_object
    )
    return owner, drawing, revision, storage


def role_user(code, name):
    user = User.objects.create_user(username=name)
    assignment = UserRole.objects.create(user=user, role=Role.objects.get(code=code))
    seed_assignment_scopes(assignment)
    return user


def login(client, user):
    client.force_login(user, backend=TEST_BACKEND)


@pytest.mark.parametrize("route", ["revision-viewer", "revision-content"])
def test_view_and_content_independently_enforce_real_scope(client, drawing_data, route):
    _, _, revision, _ = drawing_data
    url = reverse(f"drawings:{route}", args=[revision.id])
    assert client.get(url).status_code == 403
    login(client, User.objects.create_user(username=f"none-{route}"))
    assert client.get(url).status_code == 403
    login(client, role_user("incoming_quality", f"wrong-{route}"))
    assert client.get(url).status_code == 403
    login(client, role_user("plastic_quality", f"right-{route}"))
    assert client.get(url).status_code in {200, 206}
    login(client, role_user("manager", f"broad-{route}"))
    assert client.get(url).status_code in {200, 206}


def test_viewer_uses_uuid_safe_metadata_and_local_overlay(client, drawing_data, settings):
    owner, drawing, revision, storage = drawing_data
    duplicate = DrawingRevision.objects.create(
        drawing=drawing,
        revision_code="A",
        status="WITHDRAWN",
        primary_file=revision.primary_file,
        created_by=owner,
    )
    login(client, role_user("plastic_quality", "reader"))
    response = client.get(reverse("drawings:revision-viewer", args=[revision.id]))
    html = response.content.decode()
    assert str(revision.id) in html and str(duplicate.id) in html
    assert html.count("<strong>A</strong>") == 2
    assert "WITHDRAWN" in html and "DRAFT" in html
    assert "data-role=\"page-stage\"" in html
    assert "data-role=\"overlay\"" in html
    assert reverse("drawings:revision-content", args=[revision.id]) in html
    assert revision.primary_file.storage_key not in html
    assert str(settings.DRAWING_STORAGE_ROOT) not in html
    assert "cdnjs" not in html and "jsdelivr" not in html and "unpkg" not in html
    assert "/static/vendor/pdfjs/2.14.305/pdf.worker.min.js" in html
    assert "control-point" not in html.casefold()


@pytest.mark.parametrize(
    ("name", "scheme", "message"),
    [
        ("x.pdf.enc", "NONE", "Bu revizyon tarayıcıda görüntülenemiyor."),
        ("x.dwg", "NONE", "Bu revizyon tarayıcıda görüntülenemiyor."),
        ("x.dxf", "NONE", "Bu revizyon tarayıcıda görüntülenemiyor."),
        ("x.pdf", "LEGACY_AES_GCM", "Legacy şifreli teknik resim."),
    ],
)
def test_unsupported_files_are_safe_415(client, drawing_data, name, scheme, message):
    _, _, revision, _ = drawing_data
    revision.primary_file.original_name = name
    revision.primary_file.encryption_scheme = scheme
    revision.primary_file.save(update_fields=["original_name", "encryption_scheme"])
    login(client, role_user("plastic_quality", f"reader-{name}-{scheme}"))
    view = client.get(reverse("drawings:revision-viewer", args=[revision.id]))
    assert message in view.content.decode()
    content = client.get(reverse("drawings:revision-content", args=[revision.id]))
    assert content.status_code == 415


def body(response):
    return b"".join(response.streaming_content)


def assert_private_headers(response):
    assert response["Content-Type"] == "application/pdf"
    assert response["Accept-Ranges"] == "bytes"
    assert response["Cache-Control"] == "private, no-store"
    assert response["X-Content-Type-Options"] == "nosniff"


def test_full_content_is_streamed_with_forced_pdf_type(client, drawing_data):
    _, _, revision, _ = drawing_data
    login(client, role_user("plastic_quality", "full"))
    response = client.get(reverse("drawings:revision-content", args=[revision.id]))
    assert response.status_code == 200 and body(response) == PDF
    assert response["Content-Length"] == str(len(PDF))
    assert_private_headers(response)


@pytest.mark.parametrize(
    ("header", "expected", "content_range"),
    [
        ("bytes=0-9", PDF[0:10], f"bytes 0-9/{len(PDF)}"),
        ("bytes=10-", PDF[10:], f"bytes 10-{len(PDF)-1}/{len(PDF)}"),
        ("bytes=-5", PDF[-5:], f"bytes {len(PDF)-5}-{len(PDF)-1}/{len(PDF)}"),
    ],
)
def test_single_ranges(client, drawing_data, header, expected, content_range):
    _, _, revision, _ = drawing_data
    login(client, role_user("plastic_quality", header.replace("=", "")))
    response = client.get(
        reverse("drawings:revision-content", args=[revision.id]), HTTP_RANGE=header
    )
    assert response.status_code == 206 and body(response) == expected
    assert response["Content-Range"] == content_range
    assert response["Content-Length"] == str(len(expected))
    assert_private_headers(response)


@pytest.mark.parametrize(
    "header", ["garbage", "bytes=-0", "bytes=999-", "bytes=9-2", "bytes=0-1,4-5"]
)
def test_invalid_ranges_are_416(client, drawing_data, header):
    _, _, revision, _ = drawing_data
    login(client, role_user("plastic_quality", f"invalid-{abs(hash(header))}"))
    response = client.get(
        reverse("drawings:revision-content", args=[revision.id]), HTTP_RANGE=header
    )
    assert response.status_code == 416
    assert response["Content-Range"] == f"bytes */{len(PDF)}"


def test_head_has_metadata_and_no_body(client, drawing_data):
    _, _, revision, _ = drawing_data
    login(client, role_user("plastic_quality", "head"))
    response = client.head(reverse("drawings:revision-content", args=[revision.id]))
    assert response.status_code == 200 and body(response) == b""
    assert response["Content-Length"] == str(len(PDF))
    assert_private_headers(response)


def test_missing_content_is_safe_404(client, drawing_data, settings):
    _, _, revision, storage = drawing_data
    storage.remove_for_compensation(revision.primary_file.storage_key)
    login(client, role_user("plastic_quality", "missing"))
    response = client.get(reverse("drawings:revision-content", args=[revision.id]))
    assert response.status_code == 404
    assert str(settings.DRAWING_STORAGE_ROOT).encode() not in response.content


def test_inline_filename_removes_crlf_and_both_path_separators(client, drawing_data):
    _, _, revision, _ = drawing_data
    revision.primary_file.original_name = "../folder\\unsafe\r\nname.pdf"
    revision.primary_file.save(update_fields=["original_name"])
    login(client, role_user("plastic_quality", "filename"))
    response = client.head(reverse("drawings:revision-content", args=[revision.id]))
    disposition = response["Content-Disposition"]
    assert disposition.startswith("inline;")
    assert "folder" not in disposition and ".." not in disposition
    assert "\r" not in disposition and "\n" not in disposition


def test_vendored_pdfjs_distribution_files_exist():
    static = Path(__file__).parents[1] / "apps/drawings/static"
    for relative in (
        "drawings/viewer.css",
        "drawings/viewer.js",
        "vendor/pdfjs/2.14.305/pdf.min.js",
        "vendor/pdfjs/2.14.305/pdf.worker.min.js",
        "vendor/pdfjs/2.14.305/LICENSE",
        "vendor/pdfjs/2.14.305/PROVENANCE.md",
    ):
        assert (static / relative).stat().st_size > 0
