# WP-000 Legacy Profiler Runbook

## Purpose and safety boundary

This standard-library CLI inventories an authoritative **read-only copy or read-only mount**. It does not migrate, repair, normalize, merge, decrypt, or write to Django/PostgreSQL. Identity transforms in the report are independent comparison lenses for ADR review, not approved canonical rules.

The operator should mount the common legacy root read-only; that root normally contains `Data/` and `Drawings/`. Use a different writable filesystem for output. The process account should have read/traverse permission on sources and write permission only on the output parent. Do not grant it a drawing decryption key, PostgreSQL credential, or production application identity.

```bash
cd /path/to/uretim-portal
python -m tools.legacy_profiler \
  --root /mnt/legacy-data-ro \
  --output /var/secure/legacy-profile/current \
  --profile-name authoritative-2026-08-26
```

When roots are arranged differently, specify `--legacy-source-root`, `--drawing-root`, or `--sql-snapshot-root`. Keep all supplied roots below the common `--root` where possible so artifacts retain unambiguous root-relative paths. SQL snapshot/export files are inventoried as optional metadata and are never treated as a second authority. `--fail-on-read-error` makes an inaccessible source fatal; normal data-quality warnings still return success.

The tool rejects output equal to or nested below a source root. It opens source content only in binary read mode. Output is recreated deterministically and contains JSON, CSV, and `PROFILE_REPORT.md` artifacts.

## Handling the output

The full output is controlled migration evidence. It can contain safe business identifiers (TR, mold and drawing variants), root-relative filenames, and capped non-personal datetime strings. **Never upload the runtime directory, source files, business identifiers, user data, drawings, hashes of secrets, keys, credentials, connection strings, or unreviewed reports to GitHub.** `artifacts/legacy-profile/` is ignored only as a final safeguard, not as authorization to store live output in a checkout.

For architecture review, security/IT should inspect the full report in-company, then prepare a separately reviewed summary containing counts, field names, aggregate format distributions, collision counts, warning classes, and decisions needed. Remove row values, usernames, absolute paths, and business-sensitive identifiers. Do not merely copy the runtime Markdown report and call it sanitized.

## Repeat-run comparison

Archive each controlled run immutably with its command and profiler commit. Compare `source_fingerprint` in `profile_summary.json`; it is derived from sorted root-relative paths and source SHA-256 values. Equal profiler versions and equal fingerprints should produce byte-identical artifacts. If fingerprints differ, compare `file_manifest.csv` SHA-256/size rows to identify the changed source before reviewing aggregate differences.

## Data gate

IT must run the command above against the authoritative/current data estate, have the sanitized evidence reviewed by the architecture/business owners, and record the source fingerprint and review outcome. Until then `WP000_DATA_GATE_STATUS` remains `BLOCKED` and WP-003/WP-004 identity constraints must not be finalized.
