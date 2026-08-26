"""Non-destructive comparison lenses; none is a canonicalization rule."""

from collections import defaultdict
import re


def lenses(value: str) -> dict[str, str]:
    trimmed = value.strip()
    folded = trimmed.casefold()
    separated = re.sub(r"[\s_./\\-]+", "", folded)
    zero_aware = re.sub(r"(?<!\d)0+(?=\d)", "", separated)
    return {"raw_exact": value, "trimmed": trimmed, "case_folded": folded,
            "separator_normalized": separated, "leading_zero_aware": zero_aware}


def collisions(entries: list[dict[str, object]]) -> list[dict[str, object]]:
    result = []
    for lens in lenses(""):
        groups: dict[str, list[dict[str, object]]] = defaultdict(list)
        for entry in entries:
            key = lenses(str(entry["value"]))[lens]
            if key:
                groups[key].append(entry)
        for key, members in sorted(groups.items()):
            raw = sorted({str(item["value"]) for item in members})
            if len(members) > 1 and (lens == "raw_exact" or len(raw) > 1):
                result.append({"lens": lens, "comparison_value": key,
                               "occurrence_count": len(members), "raw_variants": raw,
                               "sources": sorted({str(item["source"]) for item in members})})
    return result
