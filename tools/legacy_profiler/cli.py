import argparse
import csv
import hashlib
from pathlib import Path
import shutil
import sys

from . import __version__
from .csv_profile import profile_csv
from .discovery import discover
from .file_profile import drawing_report
from .identity_profile import collisions
from .report import write_all


def _inside(path: Path, parent: Path) -> bool:
    try: path.relative_to(parent); return True
    except ValueError: return False


def run(root: Path, output: Path, data_root: Path | None = None, drawing_root: Path | None = None,
        sql_root: Path | None = None, max_samples: int = 3, profile_name: str = "legacy-profile",
        fail_on_read_error: bool = False) -> dict:
    root, output = root.resolve(), output.resolve()
    data_root = (data_root or (root / "Data" if (root / "Data").exists() else root)).resolve()
    drawing_root = (drawing_root or root / "Drawings").resolve()
    sql_root = sql_root.resolve() if sql_root else None
    if not root.is_dir(): raise ValueError("source root must be an existing directory")
    if output == root or _inside(output, root): raise ValueError("output must be outside every source root")
    for source in (data_root, drawing_root, sql_root):
        if source and _inside(output, source): raise ValueError("output must be outside every source root")
    manifest = discover(root, data_root, drawing_root, sql_root)
    csv_profiles, entries, numeric, dates, warnings = [], [], [], [], []
    by_relative = {item["relative_path"]: item for item in manifest}
    for item in manifest:
        if not item["exists"] or item["file_type"] not in ("csv", "txt", "log"): continue
        path = root / item["relative_path"]
        if not path.is_file(): continue
        try:
            profile, keys, nums, dts, warns = profile_csv(item["relative_path"], path.open("rb").read(), max_samples)
            csv_profiles.append(profile); entries.extend(keys); numeric.extend(nums); dates.extend(dts); warnings.extend(warns)
            item.update(encoding=profile["encoding"], delimiter=profile["delimiter"], headers=profile["headers"], row_count=profile["row_count"], warning_count=len(warns))
        except (OSError, csv.Error, UnicodeError) as exc:
            item["error_count"] = 1
            warnings.append({"code": "read_error", "source": item["relative_path"], "error_type": type(exc).__name__})
            if fail_on_read_error: raise
    duplicate_rows = []
    fields = sorted({str(e["field"]) for e in entries})
    for field in fields:
        for collision in collisions([e for e in entries if e["field"] == field]):
            duplicate_rows.append({"field": field, **collision})
    drawings_prefix = drawing_root.relative_to(root).as_posix() + "/" if _inside(drawing_root, root) else "external/"
    drawings = drawing_report(manifest, drawings_prefix)
    products = next((p for p in csv_profiles if p["relative_path"].casefold().endswith("products.csv")), None)
    references = []
    if products and "DrawingFile" in products["headers"]:
        refs = [e for e in entries if e["field"] == "DrawingFile"]
        names = {f["logical_name"].casefold() for f in manifest if f["relative_path"].startswith(drawings_prefix) and f["exists"]}
        blank_count = products["blank_counts"].get("DrawingFile", 0)
        matched = sum(str(e["value"]).rsplit("/", 1)[-1].casefold() in names for e in refs)
        references.append({"check_name": "product_drawing_file_exists", "source": products["relative_path"], "reference_columns": ["DrawingFile"], "candidate_target": "Drawings/** filename", "matched_count": matched, "unmatched_count": len(refs)-matched, "blank_reference_count": blank_count, "ambiguous_count": 0})
    fingerprint = hashlib.sha256("".join(f"{i['relative_path']}:{i['sha256'] or 'missing'}\n" for i in manifest).encode()).hexdigest()
    result = {"summary": {"profile_name": profile_name, "profiler_version": __version__, "source_fingerprint": fingerprint,
                           "files_present": sum(bool(i["exists"]) for i in manifest), "expected_files_missing": sum(not i["exists"] for i in manifest),
                           "csv_rows": sum(p["row_count"] for p in csv_profiles), "warning_count": len(warnings),
                           "sql_snapshot_supplied": sql_root is not None, "sql_snapshot_policy": "metadata-only; never an independent authority"},
              "manifest": manifest, "csv_profiles": csv_profiles, "duplicates": duplicate_rows,
              "references": references, "numeric": numeric, "datetime": dates, "drawings": drawings, "warnings": warnings}
    if output.exists(): shutil.rmtree(output)
    write_all(output, result)
    return result


def parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(description="Read-only, sanitized legacy data profiler")
    p.add_argument("--root", type=Path, required=True); p.add_argument("--output", type=Path, required=True)
    p.add_argument("--legacy-source-root", type=Path); p.add_argument("--drawing-root", type=Path); p.add_argument("--sql-snapshot-root", type=Path)
    p.add_argument("--max-sample-values", type=int, default=3); p.add_argument("--profile-name", default="legacy-profile")
    p.add_argument("--fail-on-read-error", action="store_true"); return p


def main(argv=None) -> int:
    args = parser().parse_args(argv)
    try:
        run(args.root, args.output, args.legacy_source_root, args.drawing_root, args.sql_snapshot_root,
            max(0, args.max_sample_values), args.profile_name, args.fail_on_read_error)
    except (OSError, ValueError) as exc:
        print(f"fatal: {exc}", file=sys.stderr); return 2
    return 0
