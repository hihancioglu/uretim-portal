# Private Drawing Storage Runbook

## Configuration

Set `DRAWING_STORAGE_BACKEND=filesystem`, `DRAWING_STORAGE_ROOT=/data/drawings`, and a positive byte limit in `DRAWING_MAX_UPLOAD_BYTES` (the example is 100 MiB). Production settings require a non-blank root. The root must be outside the checkout, `STATIC_ROOT`, and every public media directory.

## Ownership and Docker behavior

The runtime image creates `/data/drawings` as root during image construction, assigns it to the non-root `app:app` identity, and applies mode `0700` before switching permanently to `USER app`. Docker initializes a new empty named volume from that image directory, preserving its ownership and mode. Compose mounts the persistent `drawing_data` volume only into web and worker at `/data/drawings`; neither process runs as root. Nginx does not mount or serve it. Object keys are application-generated UUID fan-out paths; client names never determine paths.

CI creates a fresh named volume, starts the built image with its normal `app` user, asserts that the effective UID is non-root, writes and reads a private test object, removes that object, and removes the volume. Existing deployments whose volume ownership was changed manually must repair it once using an operator-controlled maintenance procedure; do not weaken directory permissions or run the application as root.

## Backup and restore

PostgreSQL metadata and the drawing volume form one recovery unit. Back them up at a mutually consistent recovery point and retain both under the same retention/version label. A restore drill must restore both, select a controlled sample of FileObject rows, verify object existence/size/SHA-256 directly under an authorized maintenance identity, and exercise the authenticated download endpoint. Do not publish files or internal paths to perform verification.

An operator can hash a known object inside the private runtime/backup environment and compare it with `core_file_object.sha256`; query/report only object UUID, expected size, existence, and match result. Never expose the root through Nginx, static/media configuration, directory listing, or a public URL.

## Operations and future backend

Writes use a same-directory temporary file, streaming SHA-256 calculation, and atomic rename. Service failures compensate newly stored, unreferenced objects where safe. A process crash between physical finalization and database commit can leave an orphan; a future narrowly controlled maintenance task may reconcile these.

The `apps.core.storage` factory is the future MinIO/S3 integration point. A future backend must retain opaque keys, private authorization, streaming, hash/size semantics, and compensation behavior; this WP deploys filesystem only.

Legacy `.pdf.enc` objects remain opaque bytes. The web process has no legacy key and performs no decrypt/encrypt, parsing, conversion, rendering, malware-scan claim, or content pairing.

## WP-005 production static assets

The Docker `static-collector` stage creates collectstatic output and the `static-server` image copies only that output into `/srv/static`. Nginx serves `/static/` from this immutable image content. It does not mount `drawing_data`, `/data/drawings`, a media directory, or any FileObject content. Rebuild both the runtime and static-server targets when application static assets change.
