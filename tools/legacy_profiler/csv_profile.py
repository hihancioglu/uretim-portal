"""Byte-preserving CSV/text inspection."""

import csv
from collections import Counter
from datetime import datetime
from decimal import Decimal, InvalidOperation
import re

from .redaction import is_sensitive, may_sample, normalized_name

DATE_FORMATS = ("%Y-%m-%dT%H:%M:%S%z", "%Y-%m-%d %H:%M:%S", "%d.%m.%Y %H:%M:%S",
                "%d/%m/%Y %H:%M", "%Y-%m-%d", "%d.%m.%Y", "%d/%m/%Y")
KEY_NAMES = {"trcode", "productcode", "productid", "moldcode", "drawingfile", "drawingno", "recordid"}


def decode_bytes(data: bytes) -> tuple[str, dict[str, object]]:
    bom = data.startswith(b"\xef\xbb\xbf")
    for encoding in (("utf-8-sig", "UTF-8 BOM") if bom else ("utf-8", "strict UTF-8"),
                     ("cp1254", "strict cp1254 fallback")):
        try:
            return data.decode(encoding[0], errors="strict"), {"encoding": encoding[0], "bom": bom,
                                                               "evidence": encoding[1]}
        except UnicodeDecodeError:
            continue
    return data.decode("cp1254", errors="replace"), {"encoding": "cp1254-replace", "bom": bom,
                                                       "evidence": "cp1254 with replacement", "low_confidence": True}


def _delimiter(text: str) -> tuple[str, dict[str, object]]:
    lines = [line for line in text.splitlines()[:20] if line.strip()]
    scores = {d: sum(line.count(d) for line in lines) for d in (",", ";", "\t")}
    chosen = max(scores, key=lambda d: (scores[d], {";": 2, ",": 1, "\t": 0}[d]))
    return chosen, {"scores": {repr(k): v for k, v in scores.items()},
                    "confidence": "high" if scores[chosen] and list(scores.values()).count(scores[chosen]) == 1 else "low"}


def _numeric(value: str) -> tuple[str, Decimal | None, int | None]:
    raw = value.strip().replace(" ", "")
    if not raw:
        return "blank", None, None
    if "," in raw and "." in raw:
        return "ambiguous_separators", None, None
    strategy = "decimal_comma" if "," in raw else "decimal_point" if "." in raw else "integer"
    try:
        number = Decimal(raw.replace(",", "."))
        scale = max(0, -number.as_tuple().exponent)
        return strategy, number, scale
    except InvalidOperation:
        return "unparseable", None, None


def _datetime(value: str) -> tuple[str, list[str]]:
    raw = value.strip()
    successes = []
    if raw.endswith("Z"):
        try:
            datetime.fromisoformat(raw[:-1] + "+00:00")
            successes.append("iso8601_utc")
        except ValueError:
            pass
    for fmt in DATE_FORMATS:
        try:
            datetime.strptime(raw, fmt)
            successes.append(fmt)
        except ValueError:
            pass
    if not successes:
        return "unparseable", []
    return ("explicit_utc" if raw.endswith("Z") or re.search(r"[+-]\d\d:\d\d$", raw) else "naive_europe_istanbul_candidate"), successes


def profile_csv(relative_path: str, data: bytes, max_samples: int) -> tuple[dict, list, list, list, list]:
    text, encoding = decode_bytes(data)
    delimiter, delimiter_evidence = _delimiter(text)
    rows = list(csv.reader(text.splitlines(), delimiter=delimiter))
    headers = rows[0] if rows else []
    normalized = [normalized_name(h) for h in headers]
    collisions = sorted(k for k, count in Counter(normalized).items() if k and count > 1)
    blanks = Counter({header: 0 for header in headers})
    malformed = 0
    key_entries, numeric, dates = [], [], []
    values_by_col = {h: [] for h in headers}
    for row_number, row in enumerate(rows[1:], 2):
        if len(row) != len(headers):
            malformed += 1
        for index, header in enumerate(headers):
            value = row[index] if index < len(row) else ""
            if not value.strip():
                blanks[header] += 1
            elif not is_sensitive(header):
                values_by_col[header].append(value)
            if normalized_name(header) in KEY_NAMES and value.strip():
                key_entries.append({"field": header, "value": value, "source": relative_path, "row": row_number})
    for header, values in values_by_col.items():
        name = normalized_name(header)
        if any(term in name for term in ("date", "time", "at")):
            counts, ambiguous = Counter(), 0
            for value in values:
                kind, formats = _datetime(value)
                counts[kind] += 1
                ambiguous += len(formats) > 1
            dates.append({"source": relative_path, "column": header, "counts": dict(sorted(counts.items())),
                          "multiple_plausible": ambiguous,
                          "samples": values[:max_samples] if may_sample(header) else [],
                          "naive_policy": "Europe/Istanbul candidate only"})
        if any(term in name for term in ("nominal", "tol", "limit", "value", "percent", "count", "no")):
            strategies, scales, signs = Counter(), Counter(), Counter()
            for value in values:
                strategy, number, scale = _numeric(value)
                strategies[strategy] += 1
                if scale is not None: scales[str(scale)] += 1
                if number is not None: signs["negative" if number < 0 else "positive" if number > 0 else "zero"] += 1
            numeric.append({"source": relative_path, "column": header, "strategies": dict(sorted(strategies.items())),
                            "decimal_places": dict(sorted(scales.items())), "signs": dict(sorted(signs.items()))})
    newline = "CRLF" if b"\r\n" in data else "LF" if b"\n" in data else "CR" if b"\r" in data else "none"
    column_shapes = {}
    value_distributions = {}
    for header, values in values_by_col.items():
        column_shapes[header] = {
            "nonblank_count": len(values),
            "multi_value_markers": {marker: sum(marker in value for value in values)
                                    for marker in ("|", ";", ",")},
            "sensitive": is_sensitive(header),
        }
        name = normalized_name(header)
        if may_sample(header) and any(term in name for term in ("role", "status", "result", "scope", "isactive", "iscritical")):
            counts = Counter(values)
            value_distributions[header] = [{"value": value, "count": count}
                                           for value, count in sorted(counts.items())[:max_samples]]
    profile = {"relative_path": relative_path, **encoding, "delimiter": delimiter,
               "delimiter_evidence": delimiter_evidence, "newline": newline, "headers": headers,
               "header_normalization_collisions": collisions, "row_count": max(0, len(rows) - 1),
               "malformed_row_count": malformed, "blank_counts": dict(blanks),
               "column_shapes": column_shapes, "value_distributions": value_distributions,
               "sensitive_columns": sorted(h for h in headers if is_sensitive(h))}
    warnings = ([{"code": "malformed_rows", "source": relative_path, "count": malformed}] if malformed else [])
    if collisions: warnings.append({"code": "header_normalization_collision", "source": relative_path, "columns": collisions})
    return profile, key_entries, numeric, dates, warnings
