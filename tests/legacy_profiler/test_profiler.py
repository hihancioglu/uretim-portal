import hashlib
import json
from pathlib import Path

from tools.legacy_profiler.cli import main, run
from tools.legacy_profiler.csv_profile import _numeric
from tools.legacy_profiler.identity_profile import collisions


def make_source(tmp_path: Path) -> tuple[Path, str]:
    root = tmp_path / "source"
    data, drawings = root / "Data", root / "Drawings"
    data.mkdir(parents=True); (drawings / "a").mkdir(parents=True); (drawings / "b").mkdir()
    secret = "Gercek-Sifre-123!"
    (data / "Products.csv").write_bytes(
        ("TrCode;ProductCode;MoldCode;DrawingFile;Password;CreatedAt;Nominal;LowerTol;UpperTol\r\n"
         " TR-001 ;ÜRÜN-1;K-01;missing.pdf;" + secret + ";01/02/2024 10:00;12,50;-0,10;+0,20\r\n"
         "tr001;ÜRÜN-1;K01;same.pdf;;not-a-date;12.50;-0.1;0.2\r\n"
         "TR-0001;ÜRÜN-2;K-001;too;many;columns;here;now;!;extra\r\n").encode("utf-8-sig"))
    (data / "Users.csv").write_bytes("UserName,Role,PasswordSalt\nÇağrı,Kalite,cok-gizli-tuz\n".encode("cp1254"))
    payload = b"%PDF synthetic duplicate"
    (drawings / "a" / "same.pdf").write_bytes(payload)
    (drawings / "b" / "SAME.PDF").write_bytes(payload)
    (drawings / "drawing.pdf.enc").write_bytes(b"TRDRAW2\x00synthetic")
    return root, secret


def snapshot(root: Path) -> dict[str, tuple[str, int]]:
    return {p.relative_to(root).as_posix(): (hashlib.sha256(p.read_bytes()).hexdigest(), p.stat().st_mtime_ns)
            for p in root.rglob("*") if p.is_file()}


def test_end_to_end_is_immutable_redacted_and_deterministic(tmp_path):
    root, secret = make_source(tmp_path)
    before = snapshot(root)
    out1, out2 = tmp_path / "out1", tmp_path / "out2"
    first = run(root, out1, max_samples=2)
    run(root, out2, max_samples=2)
    assert snapshot(root) == before
    files1 = {p.name: p.read_bytes() for p in out1.iterdir()}
    files2 = {p.name: p.read_bytes() for p in out2.iterdir()}
    assert files1 == files2
    all_output = b"".join(files1.values())
    assert secret.encode() not in all_output
    assert "cok-gizli-tuz".encode() not in all_output
    assert first["csv_profiles"][0]["malformed_row_count"] == 1
    assert first["drawings"]["duplicate_hashes"]
    assert first["drawings"]["format_counts"]["V2"] == 1
    assert first["references"][0]["unmatched_count"] == 2
    products = (root / "Data" / "Products.csv").read_bytes()
    manifest = {row["logical_name"]: row for row in first["manifest"]}
    assert manifest["Products.csv"]["sha256"] == hashlib.sha256(products).hexdigest()
    json.loads((out1 / "profile_summary.json").read_text())


def test_identity_lenses_are_separate_and_decimal_parser_uses_decimal():
    entries = [{"value": value, "source": "x", "field": "TrCode"} for value in (" TR-001 ", "tr001", "TR-0001")]
    found = collisions(entries)
    assert any(row["lens"] == "separator_normalized" for row in found)
    assert any(row["lens"] == "leading_zero_aware" for row in found)
    strategy, value, scale = _numeric("-12,500")
    assert strategy == "decimal_comma" and str(value) == "-12.500" and scale == 3


def test_cli_rejects_output_inside_source(tmp_path):
    root, _ = make_source(tmp_path)
    assert main(["--root", str(root), "--output", str(root / "artifacts")]) == 2
