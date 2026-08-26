import hashlib
import io
import uuid

import pytest
from django.core.exceptions import PermissionDenied, ValidationError
from django.db import IntegrityError, transaction

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.core.models import FileObject
from apps.core.storage.base import StorageError
from apps.core.storage.filesystem import FilesystemStorage
from apps.drawings.models import Drawing, DrawingRevision
from apps.drawings.services import (
    activate_revision,
    create_drawing,
    create_drawing_revision_with_file,
    update_draft_revision,
    withdraw_revision,
)
from apps.products.models import Product

pytestmark = pytest.mark.django_db


@pytest.fixture
def manager():
    seed_authorization_baseline()
    user = User.objects.create_user(username="drawing-manager")
    assignment = UserRole.objects.create(
        user=user, role=Role.objects.get(code="technical_drawing"), assigned_by=user
    )
    seed_assignment_scopes(assignment)
    return user


@pytest.fixture
def product(manager):
    return Product.objects.create(
        tr_code="TR-1", product_name="Product", created_by=manager, updated_by=manager
    )


def test_filesystem_streams_hashes_and_uses_opaque_key(tmp_path):
    storage = FilesystemStorage(tmp_path, 1000)
    payload = b"drawing bytes"
    stored = storage.store(io.BytesIO(payload), "../../unsafe.pdf")
    assert stored.size_bytes == len(payload)
    assert stored.sha256 == hashlib.sha256(payload).hexdigest()
    assert "unsafe" not in stored.storage_key and not stored.storage_key.startswith("/")
    assert storage.open(stored.storage_key).read() == payload


@pytest.mark.parametrize(("name", "payload"), [("x.exe", b"x"), ("x.pdf", b"")])
def test_storage_rejects_unsafe_uploads(tmp_path, name, payload):
    with pytest.raises(StorageError):
        FilesystemStorage(tmp_path, 100).store(io.BytesIO(payload), name)


def test_deferred_identities_and_revision_lifecycle(manager, product, tmp_path):
    first = create_drawing(actor=manager, product=product, scope="PLASTIC")
    second = create_drawing(actor=manager, product=product, scope="PLASTIC")
    assert first.id != second.id and isinstance(first.id, uuid.UUID)
    storage = FilesystemStorage(tmp_path, 1000)
    r1 = create_drawing_revision_with_file(
        actor=manager,
        drawing=first,
        revision_code="A",
        stream=io.BytesIO(b"one"),
        original_name="one.pdf",
        storage=storage,
    )
    r2 = create_drawing_revision_with_file(
        actor=manager,
        drawing=first,
        revision_code="A",
        stream=io.BytesIO(b"two"),
        original_name="two.pdf",
        storage=storage,
    )
    assert r1.status == "DRAFT" and isinstance(r1.id, uuid.UUID)
    activate_revision(actor=manager, revision=r1)
    activate_revision(actor=manager, revision=r2)
    r1.refresh_from_db()
    r2.refresh_from_db()
    assert r1.status == "SUPERSEDED" and r1.effective_to
    assert r2.status == "ACTIVE" and r2.effective_from and r2.approved_by == manager
    with pytest.raises(ValidationError):
        update_draft_revision(actor=manager, revision=r1, revision_code="B")
    withdraw_revision(actor=manager, revision=r2)
    with pytest.raises(ValidationError):
        activate_revision(actor=manager, revision=r2)


def test_database_blocks_two_active_revisions(manager, product):
    drawing = Drawing.objects.create(product=product, scope="TR")
    file_object = FileObject.objects.create(
        storage_key="objects/aa/bb/a",
        original_name="a.pdf",
        mime_type="application/pdf",
        size_bytes=1,
        sha256="a" * 64,
    )
    DrawingRevision.objects.create(
        drawing=drawing, revision_code="1", primary_file=file_object, status="ACTIVE"
    )
    with pytest.raises(IntegrityError), transaction.atomic():
        DrawingRevision.objects.create(
            drawing=drawing,
            revision_code="2",
            primary_file=file_object,
            status="ACTIVE",
        )


def test_unauthorized_management_is_denied(product):
    user = User.objects.create_user(username="unauthorized")
    with pytest.raises(PermissionDenied):
        create_drawing(actor=user, product=product, scope="TR")


def test_scope_service_boundary(manager, product):
    with pytest.raises(ValidationError):
        create_drawing(actor=manager, product=product, scope="OTHER")


def test_sha256_is_not_unique(manager):
    values = dict(
        original_name="same.pdf",
        mime_type="application/pdf",
        size_bytes=1,
        sha256="b" * 64,
        created_by=manager,
    )
    FileObject.objects.create(storage_key="objects/aa/bb/1", **values)
    FileObject.objects.create(storage_key="objects/aa/bb/2", **values)
