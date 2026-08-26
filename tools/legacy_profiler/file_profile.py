"""Safe drawing estate classification from filenames, hashes and non-secret headers."""

from collections import Counter, defaultdict
import hashlib


def drawing_report(files: list[dict], drawing_prefix: str) -> dict:
    candidates = [f for f in files if f["relative_path"].startswith(drawing_prefix) and f["exists"]]
    extensions, names, hashes, formats = Counter(), defaultdict(list), defaultdict(list), Counter()
    allowed = {".pdf", ".enc", ".dwg", ".dxf"}
    abnormal = []
    for item in candidates:
        path = item["relative_path"]
        lower = path.casefold()
        suffix = ".pdf.enc" if lower.endswith(".pdf.enc") else "." + lower.rsplit(".", 1)[-1] if "." in lower else "none"
        extensions[suffix] += 1
        names[path.rsplit("/", 1)[-1].casefold()].append(path)
        hashes[item["sha256"]].append(path)
        if suffix in (".enc", ".pdf.enc"):
            header = bytes.fromhex(item.get("header_hex", ""))
            formats["V2" if header.startswith(b"TRDRAW2") else "V1_or_unknown"] += 1
        elif suffix == ".pdf": formats["plain_pdf"] += 1
        if suffix not in allowed and suffix != ".pdf.enc": abnormal.append(path)
    return {"candidate_file_count": len(candidates), "extension_counts": dict(sorted(extensions.items())),
            "format_counts": dict(sorted(formats.items())),
            "duplicate_filenames": {k: v for k, v in sorted(names.items()) if len(v) > 1},
            "duplicate_hashes": {k: v for k, v in sorted(hashes.items()) if len(v) > 1},
            "zero_byte_files": sorted(f["relative_path"] for f in candidates if f["size_bytes"] == 0),
            "abnormal_extensions": sorted(abnormal)}
