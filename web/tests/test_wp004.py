import hashlib
import io
import uuid

import pytest
from django.core.exceptions import PermissionDenied, ValidationError
from django.db import IntegrityError, transaction
from django.urls import reverse

from apps.accounts.models import Role, User, UserRole
from apps.accounts.seeding import seed_assignment_scopes, seed_authorization_baseline
from apps.audit.models import AuditEvent
from apps.core.models import FileObject
from apps.core.storage.base import StorageError
from apps.core.storage.filesystem import FilesystemStorage
from apps.drawings.models import Drawing, DrawingRevision
from apps.drawings.services import (
    activate_revision,
    create_drawing,
    create_drawing_revision_with_file,
    deactivate_drawing,
    replace_draft_revision_file,
    update_drawing,
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


def test_draft_file_replacement_is_atomic_and_preserves_old_file(
    manager, product, tmp_path
):
    storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    revision = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"old"),
        original_name="old.pdf",
        storage=storage,
    )
    old_file = revision.primary_file
    replaced = replace_draft_revision_file(
        actor=manager,
        revision=revision,
        stream=io.BytesIO(b"new"),
        original_name="new.pdf",
        storage=storage,
    )
    assert replaced.primary_file_id != old_file.id
    assert FileObject.objects.filter(pk=old_file.id).exists()
    assert storage.open(old_file.storage_key).read() == b"old"
    assert AuditEvent.objects.filter(
        event_type="drawing_revision.file_replaced", entity_id=str(revision.id)
    ).exists()


def test_replacement_revalidates_after_upload_and_compensates(
    manager, product, tmp_path
):
    base_storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    revision = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"old"),
        original_name="old.pdf",
        storage=base_storage,
    )
    before_files = FileObject.objects.count()

    class ActivatingStorage(FilesystemStorage):
        stored_key = None

        def store(self, stream, original_name):
            stored = super().store(stream, original_name)
            self.stored_key = stored.storage_key
            DrawingRevision.objects.filter(pk=revision.pk).update(status="ACTIVE")
            return stored

    storage = ActivatingStorage(tmp_path, 1000)
    with pytest.raises(ValidationError):
        replace_draft_revision_file(
            actor=manager,
            revision=revision,
            stream=io.BytesIO(b"orphan candidate"),
            original_name="new.pdf",
            storage=storage,
        )
    assert FileObject.objects.count() == before_files
    assert not storage.exists(storage.stored_key)
    assert not AuditEvent.objects.filter(
        event_type="drawing_revision.file_replaced", entity_id=str(revision.id)
    ).exists()


@pytest.mark.parametrize("status", ["ACTIVE", "SUPERSEDED", "WITHDRAWN"])
def test_non_draft_replacement_is_rejected_and_compensated(
    manager, product, tmp_path, status
):
    storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    revision = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"old"),
        original_name="old.pdf",
        storage=storage,
    )
    DrawingRevision.objects.filter(pk=revision.pk).update(status=status)
    before_files = FileObject.objects.count()
    with pytest.raises(ValidationError):
        replace_draft_revision_file(
            actor=manager,
            revision=revision,
            stream=io.BytesIO(b"new"),
            original_name="new.pdf",
            storage=storage,
        )
    assert FileObject.objects.count() == before_files


def test_unsupported_encryption_metadata_is_rejected_before_storage(
    manager, product, tmp_path
):
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    with pytest.raises(ValidationError):
        create_drawing_revision_with_file(
            actor=manager,
            drawing=drawing,
            revision_code="A",
            stream=io.BytesIO(b"bytes"),
            original_name="drawing.pdf",
            encryption_scheme="UNSUPPORTED",
            storage=FilesystemStorage(tmp_path, 1000),
        )
    assert not any(tmp_path.rglob("*.*"))


def _role_user(username, role_code):
    user = User.objects.create_user(username=username)
    assignment = UserRole.objects.create(
        user=user, role=Role.objects.get(code=role_code), assigned_by=user
    )
    seed_assignment_scopes(assignment)
    return user


def test_private_file_endpoint_authorization_scope_and_headers(
    client, manager, product, tmp_path, settings
):
    settings.DRAWING_STORAGE_ROOT = str(tmp_path)
    storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="PLASTIC")
    revision = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"exact private bytes"),
        original_name="../../unsafe\r\nHeader.pdf",
        storage=storage,
    )
    url = reverse("drawings:revision-file", args=[revision.id])
    assert client.get(url).status_code == 403
    seed_authorization_baseline()
    no_role = User.objects.create_user(username="no-role")
    client.force_login(no_role)
    assert client.get(url).status_code == 403
    wrong_scope = _role_user("incoming", "incoming_quality")
    client.force_login(wrong_scope)
    assert client.get(url).status_code == 403
    scoped_reader = _role_user("plastic-reader", "plastic_quality")
    client.force_login(scoped_reader)
    response = client.get(url)
    assert response.status_code == 200
    assert b"".join(response.streaming_content) == b"exact private bytes"
    broad_reader = _role_user("manager-reader", "manager")
    client.force_login(broad_reader)
    response = client.get(url)
    assert response.status_code == 200
    assert b"".join(response.streaming_content) == b"exact private bytes"
    assert response["Content-Disposition"].startswith("attachment;")
    assert ".." not in response["Content-Disposition"]
    assert "\r" not in response["Content-Disposition"]
    assert "\n" not in response["Content-Disposition"]
    assert response["Cache-Control"] == "private, no-store"
    assert response["X-Content-Type-Options"] == "nosniff"
    headers = str(response.headers)
    assert str(tmp_path) not in headers
    assert revision.primary_file.storage_key not in headers


def test_private_file_endpoint_returns_404_for_missing_object(
    client, manager, product, tmp_path, settings
):
    settings.DRAWING_STORAGE_ROOT = str(tmp_path)
    storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    revision = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"bytes"),
        original_name="drawing.pdf",
        storage=storage,
    )
    storage.remove_for_compensation(revision.primary_file.storage_key)
    client.force_login(manager)
    assert (
        client.get(reverse("drawings:revision-file", args=[revision.id])).status_code
        == 404
    )


def test_mutation_audits_include_supersession(manager, product, tmp_path):
    storage = FilesystemStorage(tmp_path, 1000)
    drawing = create_drawing(actor=manager, product=product, scope="TR")
    update_drawing(actor=manager, drawing=drawing, title="Updated")
    first = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="A",
        stream=io.BytesIO(b"a"),
        original_name="a.pdf",
        storage=storage,
    )
    update_draft_revision(actor=manager, revision=first, change_reason="Approved")
    activate_revision(actor=manager, revision=first)
    second = create_drawing_revision_with_file(
        actor=manager,
        drawing=drawing,
        revision_code="B",
        stream=io.BytesIO(b"b"),
        original_name="b.pdf",
        storage=storage,
    )
    activate_revision(actor=manager, revision=second)
    withdraw_revision(actor=manager, revision=second)
    deactivate_drawing(actor=manager, drawing=drawing)
    event_types = list(AuditEvent.objects.values_list("event_type", flat=True))
    for expected in (
        "drawing.created",
        "drawing.updated",
        "drawing.deactivated",
        "drawing_revision.created",
        "drawing_revision.updated_draft",
        "drawing_revision.activated",
        "drawing_revision.superseded",
        "drawing_revision.withdrawn",
        "file_object.created",
    ):
        assert expected in event_types
    assert event_types.count("drawing_revision.activated") == 2


def test_failed_transaction_does_not_leave_success_audit(manager, product, monkeypatch):
    def fail_after_write(*args, **kwargs):
        raise RuntimeError("forced audit failure")

    monkeypatch.setattr("apps.drawings.services._audit", fail_after_write)
    with pytest.raises(RuntimeError):
        create_drawing(actor=manager, product=product, scope="TR")
    assert not Drawing.objects.filter(product=product, scope="TR").exists()
    assert not AuditEvent.objects.filter(event_type="drawing.created").exists()
