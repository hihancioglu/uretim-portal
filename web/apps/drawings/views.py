import re

from django.http import FileResponse, Http404, HttpResponse, StreamingHttpResponse
from django.shortcuts import get_object_or_404, render
from django.urls import reverse
from django.utils.http import content_disposition_header

from apps.accounts.authz import has_scoped_action, require_scoped_action
from apps.core.storage import drawing_storage

from .models import DrawingRevision

_RANGE = re.compile(r"^bytes=(\d*)-(\d*)$")


def _authorized_revision(request, revision_id):
    revision = get_object_or_404(
        DrawingRevision.objects.select_related("drawing", "primary_file"),
        pk=revision_id,
    )
    require_scoped_action(
        request.user,
        "drawings.view",
        scope_type="DRAWING",
        scope_key=revision.drawing.scope,
    )
    return revision


def _safe_filename(name):
    cleaned = str(name).replace("\r", "").replace("\n", "")
    cleaned = cleaned.replace("\\", "/").rsplit("/", 1)[-1]
    return cleaned.strip() or "drawing.pdf"


def _is_renderable(file_object):
    return (
        file_object.encryption_scheme == file_object.EncryptionScheme.NONE
        and file_object.original_name.casefold().endswith(".pdf")
    )


def _content_headers(response, *, size, filename, content_range=None):
    response["Accept-Ranges"] = "bytes"
    response["Content-Length"] = str(size)
    response["Cache-Control"] = "private, no-store"
    response["X-Content-Type-Options"] = "nosniff"
    response["Content-Disposition"] = content_disposition_header(False, filename)
    if content_range:
        response["Content-Range"] = content_range
    return response


def _parse_range(value, size):
    match = _RANGE.fullmatch(value or "")
    if not match or "," in value:
        raise ValueError
    first, last = match.groups()
    if not first and not last:
        raise ValueError
    if not first:
        suffix = int(last)
        if suffix <= 0 or size <= 0:
            raise ValueError
        start = max(0, size - suffix)
        return start, size - 1
    start = int(first)
    if start >= size:
        raise ValueError
    end = size - 1 if not last else int(last)
    if end < start:
        raise ValueError
    return start, min(end, size - 1)


def revision_viewer(request, revision_id):
    revision = _authorized_revision(request, revision_id)
    revisions = revision.drawing.revisions.order_by("created_at", "id")
    file_object = revision.primary_file
    unsupported_reason = ""
    if file_object.encryption_scheme == file_object.EncryptionScheme.LEGACY_AES_GCM:
        unsupported_reason = (
            "Legacy şifreli teknik resim. Web görüntüleme desteği henüz etkin değil."
        )
    elif not _is_renderable(file_object):
        unsupported_reason = "Bu revizyon tarayıcıda görüntülenemiyor."
    elif not drawing_storage().exists(file_object.storage_key):
        unsupported_reason = "Teknik resim dosyası bulunamadı."
    has_management_access = has_scoped_action(
        request.user,
        "drawings.manage",
        scope_type="DRAWING",
        scope_key=revision.drawing.scope,
    )
    return render(
        request,
        "drawings/revision_viewer.html",
        {
            "revision": revision,
            "revisions": revisions,
            "renderable": not unsupported_reason,
            "unsupported_reason": unsupported_reason,
            "content_url": reverse("drawings:revision-content", args=[revision.id]),
            "download_url": reverse("drawings:revision-file", args=[revision.id]),
            "can_manage_control_points": revision.status
            in {
                DrawingRevision.Status.DRAFT,
                DrawingRevision.Status.ACTIVE,
            }
            and has_management_access,
            "has_management_access": has_management_access,
            "control_points_url": reverse("control_points:list", args=[revision.id]),
            "control_point_create_url": reverse(
                "control_points:create", args=[revision.id]
            ),
            "control_point_copy_url": reverse(
                "control_points:copy", args=[revision.id]
            ),
            "management_url": reverse(
                "drawings:manage-drawing-detail", args=[revision.drawing_id]
            ),
        },
    )


def revision_content(request, revision_id):
    revision = _authorized_revision(request, revision_id)
    file_object = revision.primary_file
    if not _is_renderable(file_object):
        response = HttpResponse("Bu içerik tarayıcıda görüntülenemez.", status=415)
        response["Cache-Control"] = "private, no-store"
        response["X-Content-Type-Options"] = "nosniff"
        return response
    storage = drawing_storage()
    try:
        size = storage.size(file_object.storage_key)
    except (OSError, ValueError):
        raise Http404
    filename = _safe_filename(file_object.original_name)
    range_value = request.headers.get("Range")
    if range_value:
        try:
            start, end = _parse_range(range_value, size)
        except (TypeError, ValueError):
            response = HttpResponse(status=416, content_type="application/pdf")
            response["Content-Range"] = f"bytes */{size}"
            response["Accept-Ranges"] = "bytes"
            response["Cache-Control"] = "private, no-store"
            response["X-Content-Type-Options"] = "nosniff"
            return response
        length = end - start + 1
        body = (
            ()
            if request.method == "HEAD"
            else storage.iter_range(file_object.storage_key, start, length)
        )
        response = StreamingHttpResponse(
            body, status=206, content_type="application/pdf"
        )
        return _content_headers(
            response,
            size=length,
            filename=filename,
            content_range=f"bytes {start}-{end}/{size}",
        )
    body = (
        () if request.method == "HEAD" else storage.iter_range(file_object.storage_key)
    )
    response = StreamingHttpResponse(body, content_type="application/pdf")
    return _content_headers(response, size=size, filename=filename)


def revision_file(request, revision_id):
    revision = _authorized_revision(request, revision_id)
    storage = drawing_storage()
    if not storage.exists(revision.primary_file.storage_key):
        raise Http404
    try:
        stream = storage.open(revision.primary_file.storage_key)
    except (OSError, ValueError) as exc:
        raise Http404 from exc
    filename = _safe_filename(revision.primary_file.original_name)
    response = FileResponse(stream, content_type=revision.primary_file.mime_type)
    response["Content-Disposition"] = content_disposition_header(True, filename)
    response["Cache-Control"] = "private, no-store"
    response["X-Content-Type-Options"] = "nosniff"
    return response
