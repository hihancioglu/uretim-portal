from dataclasses import dataclass


@dataclass(frozen=True)
class StoredObject:
    storage_key: str
    size_bytes: int
    sha256: str


class StorageError(ValueError):
    pass
