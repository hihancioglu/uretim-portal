import hashlib
import os
import tempfile
import uuid
from pathlib import Path, PurePosixPath

from .base import StoredObject, StorageError

ALLOWED_SUFFIXES = (".pdf.enc", ".pdf", ".dwg", ".dxf")


class FilesystemStorage:
    def __init__(self, root, max_upload_bytes):
        self.root = Path(root).resolve()
        self.max_upload_bytes = max_upload_bytes

    def _path(self, key):
        pure = PurePosixPath(key)
        if pure.is_absolute() or ".." in pure.parts or not key.startswith("objects/"):
            raise StorageError("Invalid storage key")
        path = (self.root / Path(*pure.parts)).resolve()
        if not path.is_relative_to(self.root):
            raise StorageError("Invalid storage key")
        return path

    def store(self, stream, original_name):
        name = str(original_name)
        if not any(name.casefold().endswith(ext) for ext in ALLOWED_SUFFIXES):
            raise StorageError("Unsupported drawing file extension")
        object_id = uuid.uuid4().hex
        key = f"objects/{object_id[:2]}/{object_id[2:4]}/{object_id}"
        target = self._path(key)
        target.parent.mkdir(parents=True, exist_ok=True)
        digest, size, temporary = hashlib.sha256(), 0, None
        try:
            with tempfile.NamedTemporaryFile(
                dir=target.parent, prefix=".upload-", delete=False
            ) as output:
                temporary = Path(output.name)
                while chunk := stream.read(1024 * 1024):
                    size += len(chunk)
                    if size > self.max_upload_bytes:
                        raise StorageError("Drawing exceeds maximum upload size")
                    digest.update(chunk)
                    output.write(chunk)
                output.flush()
                os.fsync(output.fileno())
            if size == 0:
                raise StorageError("Empty drawings are not allowed")
            os.replace(temporary, target)
            temporary = None
            return StoredObject(key, size, digest.hexdigest())
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)

    def open(self, storage_key):
        return self._path(storage_key).open("rb")

    def size(self, storage_key):
        """Return the physical object size without exposing its path."""
        return self._path(storage_key).stat().st_size

    def iter_range(self, storage_key, start=0, length=None, chunk_size=64 * 1024):
        """Yield a bounded portion of an object and always close the handle."""
        stream = self.open(storage_key)
        try:
            stream.seek(start)
            remaining = length
            while remaining is None or remaining > 0:
                requested = chunk_size if remaining is None else min(chunk_size, remaining)
                chunk = stream.read(requested)
                if not chunk:
                    break
                yield chunk
                if remaining is not None:
                    remaining -= len(chunk)
        finally:
            stream.close()

    def exists(self, storage_key):
        return self._path(storage_key).is_file()

    def remove_for_compensation(self, storage_key):
        self._path(storage_key).unlink(missing_ok=True)
