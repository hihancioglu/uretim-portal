from pathlib import Path

from django.http import FileResponse, Http404
from django.shortcuts import get_object_or_404
from django.utils.http import content_disposition_header

from apps.accounts.authz import require_scoped_action
from apps.core.storage import drawing_storage

from .models import DrawingRevision


def revision_file(request, revision_id):
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
    storage = drawing_storage()
    if not storage.exists(revision.primary_file.storage_key):
        raise Http404
    try:
        stream = storage.open(revision.primary_file.storage_key)
    except (OSError, ValueError) as exc:
        raise Http404 from exc
    filename = (
        Path(
            revision.primary_file.original_name.replace("\r", "").replace("\n", "")
        ).name
        or "drawing"
    )
    response = FileResponse(stream, content_type=revision.primary_file.mime_type)
    response["Content-Disposition"] = content_disposition_header(True, filename)
    response["Cache-Control"] = "private, no-store"
    response["X-Content-Type-Options"] = "nosniff"
    return response
