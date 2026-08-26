from pathlib import Path
import hashlib

EXPECTED = ("Users.csv", "Products.csv", "ControlPoints.csv", "MeasurementGroupAreas.csv",
            "MeasurementRecords.csv", "MeasurementCorrections.csv", "SpcLimitCorrections.csv",
            "VisualControlRecords.csv", "ClosedEyeRecords.csv", "AuditLog.csv", "ProductionTickets.csv",
            "MoldBindingRecords.csv", "MoldTickets.csv", "QualityToProductionTickets.csv",
            "MoldConnectionPlan.csv", "NewMoldCommissionings.csv", "TestRequestRecords.csv",
            "TestCatalog.csv", "TestGroups.csv", "MeasurementDevices.csv", "INO_Database.csv")


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def discover(root: Path, data_root: Path, drawing_root: Path, sql_root: Path | None) -> list[dict]:
    paths = {p for base in (data_root, drawing_root, sql_root) if base and base.exists() for p in base.rglob("*") if p.is_file()}
    for name in EXPECTED:
        paths.add(data_root / name)
    result = []
    for path in sorted(paths, key=lambda p: p.as_posix().casefold()):
        exists = path.is_file()
        try: relative = path.relative_to(root).as_posix()
        except ValueError: relative = f"external/{path.name}"
        item = {"logical_name": path.name, "relative_path": relative, "exists": exists,
                "file_type": path.suffix.casefold().lstrip(".") or "binary", "size_bytes": None,
                "sha256": None, "last_modified_time": None, "warning_count": 0, "error_count": 0}
        if exists:
            stat = path.stat()
            item.update(size_bytes=stat.st_size, sha256=sha256_file(path),
                        last_modified_time=stat.st_mtime_ns, header_hex=path.open("rb").read(16).hex())
        result.append(item)
    return result
