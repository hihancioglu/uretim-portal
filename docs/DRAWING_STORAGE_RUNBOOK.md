# Private Drawing Storage Runbook

## Configuration

Set `DRAWING_STORAGE_BACKEND=filesystem`, `DRAWING_STORAGE_ROOT=/data/drawings`, and a positive byte limit in `DRAWING_MAX_UPLOAD_BYTES` (the example is 100 MiB). Production settings require a non-blank root. The root must be outside the checkout, `STATIC_ROOT`, and every public media directory.

## Ownership and Docker behavior

Create the directory for the application UID/GID with read, write, and traverse permission for that identity only (typically mode `0700`; operational backup identities may receive separately controlled access). Compose mounts the persistent `drawing_data` volume only into web and worker at `/data/drawings`. Nginx does not mount or serve it. Object keys are application-generated UUID fan-out paths; client names never determine paths.

## Backup and restore

PostgreSQL metadata and the drawing volume form one recovery unit. Back them up at a mutually consistent recovery point and retain both under the same retention/version label. A restore drill must restore both, select a controlled sample of FileObject rows, verify object existence/size/SHA-256 directly under an authorized maintenance identity, and exercise the authenticated download endpoint. Do not publish files or internal paths to perform verification.

An operator can hash a known object inside the private runtime/backup environment and compare it with `core_file_object.sha256`; query/report only object UUID, expected size, existence, and match result. Never expose the root through Nginx, static/media configuration, directory listing, or a public URL.

## Operations and future backend

Writes use a same-directory temporary file, streaming SHA-256 calculation, and atomic rename. Service failures compensate newly stored, unreferenced objects where safe. A process crash between physical finalization and database commit can leave an orphan; a future narrowly controlled maintenance task may reconcile these.

The `apps.core.storage` factory is the future MinIO/S3 integration point. A future backend must retain opaque keys, private authorization, streaming, hash/size semantics, and compensation behavior; this WP deploys filesystem only.

Legacy `.pdf.enc` objects remain opaque bytes. The web process has no legacy key and performs no decrypt/encrypt, parsing, conversion, rendering, malware-scan claim, or content pairing.
