import csv
import json
from pathlib import Path


def write_json(path: Path, value: object) -> None:
    path.write_text(json.dumps(value, ensure_ascii=False, sort_keys=True, indent=2) + "\n", encoding="utf-8")


def write_csv(path: Path, rows: list[dict], fields: list[str]) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow({k: json.dumps(v, ensure_ascii=False, sort_keys=True) if isinstance(v, (list, dict)) else v for k, v in row.items()})


def write_all(output: Path, result: dict) -> None:
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "profile_summary.json", result["summary"])
    write_json(output / "csv_profiles.json", result["csv_profiles"])
    write_json(output / "numeric_profile.json", result["numeric"])
    write_json(output / "datetime_profile.json", result["datetime"])
    write_json(output / "drawing_file_profile.json", result["drawings"])
    write_json(output / "warnings.json", result["warnings"])
    write_csv(output / "file_manifest.csv", result["manifest"], ["logical_name", "relative_path", "exists", "file_type", "size_bytes", "sha256", "last_modified_time", "encoding", "delimiter", "headers", "row_count", "warning_count", "error_count"])
    write_csv(output / "candidate_key_duplicates.csv", result["duplicates"], ["field", "lens", "comparison_value", "occurrence_count", "raw_variants", "sources"])
    write_csv(output / "reference_checks.csv", result["references"], ["check_name", "source", "reference_columns", "candidate_target", "matched_count", "unmatched_count", "blank_reference_count", "ambiguous_count"])
    summary = result["summary"]
    (output / "PROFILE_REPORT.md").write_text(
        "# Legacy Profile Report\n\nStructural evidence only; comparison lenses are not canonicalization rules.\n\n"
        f"- Profiler version: `{summary['profiler_version']}`\n- Files present: {summary['files_present']}\n"
        f"- CSV rows: {summary['csv_rows']}\n- Warnings: {summary['warning_count']}\n"
        f"- Source fingerprint: `{summary['source_fingerprint']}`\n- Authoritative SQL role: `{summary['sql_snapshot_policy']}`\n",
        encoding="utf-8")
