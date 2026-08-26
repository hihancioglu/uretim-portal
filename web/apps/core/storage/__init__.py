from django.conf import settings

from .filesystem import FilesystemStorage


def drawing_storage():
    if settings.DRAWING_STORAGE_BACKEND != "filesystem":
        raise RuntimeError("Unsupported drawing storage backend")
    return FilesystemStorage(
        settings.DRAWING_STORAGE_ROOT, settings.DRAWING_MAX_UPLOAD_BYTES
    )


__all__ = ["drawing_storage", "FilesystemStorage"]
