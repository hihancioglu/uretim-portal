import mimetypes

from django.core.exceptions import ValidationError
from django.db import transaction
from django.utils import timezone

from apps.accounts.authz import require_action
from apps.audit.services import create_audit_event
from apps.core.models import FileObject
from apps.core.storage import drawing_storage

from .models import Drawing, DrawingRevision

MANAGE = "drawings.manage"
_UNSET = object()


def _text(value, field):
    if not isinstance(value, str) or not value.strip():
        raise ValidationError({field: "This field is required."})


def _validate_encryption_scheme(value):
    if value not in FileObject.EncryptionScheme.values:
        raise ValidationError({"encryption_scheme": "Unsupported encryption scheme."})


def _audit(actor, event_type, entity, metadata=None):
    create_audit_event(
        actor=actor,
        actor_snapshot=actor.get_username(),
        event_type=event_type,
        entity_type=entity._meta.label_lower,
        entity_id=str(entity.id),
        metadata=metadata or {},
    )


@transaction.atomic
def create_drawing(*, actor, product, scope, title=""):
    require_action(actor, MANAGE)
    if scope not in Drawing.Scope.values:
        raise ValidationError({"scope": "Unsupported drawing scope."})
    drawing = Drawing.objects.create(
        product=product, scope=scope, title=title, created_by=actor, updated_by=actor
    )
    _audit(
        actor,
        "drawing.created",
        drawing,
        {"product_id": str(product.id), "scope": scope},
    )
    return drawing


@transaction.atomic
def update_drawing(*, actor, drawing, title=_UNSET):
    require_action(actor, MANAGE)
    drawing = Drawing.objects.select_for_update().get(pk=drawing.pk)
    before = drawing.title
    if title is not _UNSET:
        drawing.title = title
    drawing.updated_by = actor
    drawing.save(update_fields=("title", "updated_by", "updated_at"))
    _audit(
        actor,
        "drawing.updated",
        drawing,
        {"old_title": before, "new_title": drawing.title},
    )
    return drawing


@transaction.atomic
def deactivate_drawing(*, actor, drawing):
    require_action(actor, MANAGE)
    drawing = Drawing.objects.select_for_update().get(pk=drawing.pk)
    drawing.is_active = False
    drawing.updated_by = actor
    drawing.save(update_fields=("is_active", "updated_by", "updated_at"))
    _audit(actor, "drawing.deactivated", drawing)
    return drawing


def create_drawing_revision_with_file(
    *,
    actor,
    drawing,
    revision_code,
    stream,
    original_name,
    mime_type="",
    encryption_scheme=FileObject.EncryptionScheme.NONE,
    change_reason="",
    storage=None,
):
    require_action(actor, MANAGE)
    _text(revision_code, "revision_code")
    _validate_encryption_scheme(encryption_scheme)
    storage = storage or drawing_storage()
    stored = storage.store(stream, original_name)
    try:
        with transaction.atomic():
            file_object = FileObject.objects.create(
                storage_key=stored.storage_key,
                original_name=original_name,
                mime_type=mime_type
                or mimetypes.guess_type(original_name)[0]
                or "application/octet-stream",
                size_bytes=stored.size_bytes,
                sha256=stored.sha256,
                encryption_scheme=encryption_scheme,
                created_by=actor,
            )
            _audit(
                actor,
                "file_object.created",
                file_object,
                {
                    "file_object_id": str(file_object.id),
                    "sha256": stored.sha256,
                    "size_bytes": stored.size_bytes,
                },
            )
            revision = DrawingRevision.objects.create(
                drawing=drawing,
                revision_code=revision_code,
                primary_file=file_object,
                change_reason=change_reason,
                created_by=actor,
                updated_by=actor,
            )
            _audit(
                actor,
                "drawing_revision.created",
                revision,
                {
                    "drawing_id": str(drawing.id),
                    "revision_code": revision_code,
                    "file_object_id": str(file_object.id),
                },
            )
            return revision
    except Exception:
        storage.remove_for_compensation(stored.storage_key)
        raise


@transaction.atomic
def update_draft_revision(
    *, actor, revision, revision_code=_UNSET, change_reason=_UNSET
):
    require_action(actor, MANAGE)
    revision = DrawingRevision.objects.select_for_update().get(pk=revision.pk)
    if revision.status != DrawingRevision.Status.DRAFT:
        raise ValidationError("Only draft revisions are editable.")
    if revision_code is not _UNSET:
        _text(revision_code, "revision_code")
        revision.revision_code = revision_code
    if change_reason is not _UNSET:
        revision.change_reason = change_reason
    revision.updated_by = actor
    revision.save(
        update_fields=("revision_code", "change_reason", "updated_by", "updated_at")
    )
    _audit(
        actor,
        "drawing_revision.updated_draft",
        revision,
        {"revision_code": revision.revision_code},
    )
    return revision


def replace_draft_revision_file(
    *,
    actor,
    revision,
    stream,
    original_name,
    mime_type="",
    encryption_scheme=FileObject.EncryptionScheme.NONE,
    storage=None,
):
    require_action(actor, MANAGE)
    _validate_encryption_scheme(encryption_scheme)
    storage = storage or drawing_storage()
    stored = storage.store(stream, original_name)
    try:
        with transaction.atomic():
            locked = DrawingRevision.objects.select_for_update().get(pk=revision.pk)
            if locked.status != DrawingRevision.Status.DRAFT:
                raise ValidationError("Only draft revisions are editable.")
            new_file = FileObject.objects.create(
                storage_key=stored.storage_key,
                original_name=original_name,
                mime_type=mime_type
                or mimetypes.guess_type(original_name)[0]
                or "application/octet-stream",
                size_bytes=stored.size_bytes,
                sha256=stored.sha256,
                encryption_scheme=encryption_scheme,
                created_by=actor,
            )
            _audit(
                actor,
                "file_object.created",
                new_file,
                {
                    "file_object_id": str(new_file.id),
                    "sha256": stored.sha256,
                    "size_bytes": stored.size_bytes,
                },
            )
            old_id = locked.primary_file_id
            locked.primary_file = new_file
            locked.updated_by = actor
            locked.save(update_fields=("primary_file", "updated_by", "updated_at"))
            _audit(
                actor,
                "drawing_revision.file_replaced",
                locked,
                {
                    "old_file_object_id": str(old_id),
                    "file_object_id": str(new_file.id),
                },
            )
            return locked
    except Exception:
        storage.remove_for_compensation(stored.storage_key)
        raise


@transaction.atomic
def activate_revision(*, actor, revision):
    require_action(actor, MANAGE)
    revision = DrawingRevision.objects.select_related("drawing").get(pk=revision.pk)
    Drawing.objects.select_for_update().get(pk=revision.drawing_id)
    revision = DrawingRevision.objects.select_for_update().get(pk=revision.pk)
    if revision.status != DrawingRevision.Status.DRAFT:
        raise ValidationError("Only a draft revision can be activated.")
    now = timezone.now()
    current = (
        DrawingRevision.objects.select_for_update()
        .filter(drawing_id=revision.drawing_id, status=DrawingRevision.Status.ACTIVE)
        .exclude(pk=revision.pk)
        .first()
    )
    if current:
        current.status = DrawingRevision.Status.SUPERSEDED
        current.effective_to = now
        current.updated_by = actor
        current.save(
            update_fields=("status", "effective_to", "updated_by", "updated_at")
        )
        _audit(
            actor,
            "drawing_revision.superseded",
            current,
            {"old_status": "ACTIVE", "new_status": "SUPERSEDED"},
        )
    revision.status = DrawingRevision.Status.ACTIVE
    revision.effective_from = revision.approved_at = now
    revision.approved_by = revision.updated_by = actor
    revision.save(
        update_fields=(
            "status",
            "effective_from",
            "approved_at",
            "approved_by",
            "updated_by",
            "updated_at",
        )
    )
    _audit(
        actor,
        "drawing_revision.activated",
        revision,
        {"old_status": "DRAFT", "new_status": "ACTIVE"},
    )
    return revision


@transaction.atomic
def withdraw_revision(*, actor, revision):
    require_action(actor, MANAGE)
    revision = DrawingRevision.objects.select_for_update().get(pk=revision.pk)
    if revision.status == DrawingRevision.Status.WITHDRAWN:
        raise ValidationError("Revision is already withdrawn.")
    old = revision.status
    now = timezone.now()
    revision.status = DrawingRevision.Status.WITHDRAWN
    if old == DrawingRevision.Status.ACTIVE:
        revision.effective_to = now
    revision.updated_by = actor
    revision.save(update_fields=("status", "effective_to", "updated_by", "updated_at"))
    _audit(
        actor,
        "drawing_revision.withdrawn",
        revision,
        {"old_status": old, "new_status": "WITHDRAWN"},
    )
    return revision
