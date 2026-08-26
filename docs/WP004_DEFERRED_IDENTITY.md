# WP-004 Deferred Drawing Identity Decisions

**Date:** 2026-08-26  
**Gate:** The authoritative/current WP-000 data profile has not been approved.

The production schema therefore uses UUID technical identities and deliberately permits multiple Drawing rows for one Product and scope, and duplicate revision codes within a Drawing. The following questions require sanitized profile evidence and an approved ADR; none is a normalization or merge rule in WP-004.

## Drawing identity — Product + Scope
It is unknown whether Product + Scope represents exactly one logical Drawing. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Revision identity
It is unknown whether `revision_code` is unique within a logical Drawing. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Legacy filename identity/equivalence
Filenames are display/migration evidence, not identities. Equivalence rules remain unknown. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Revision-code case sensitivity
No case folding is applied. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Revision-code whitespace normalization
Blank-only codes are rejected, but accepted values are neither trimmed nor normalized. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## TR/Product duplicates
The effect of duplicate TR/Product candidates on Drawing identity is unknown. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Encrypted/plain pairing
No automatic `.pdf.enc`/plaintext pairing or equivalence is inferred. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## CAD/PDF pairing
No DWG/DXF/PDF pairing rule is inferred from names or locations. `STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

The approved, non-deferred target invariant is **at most one ACTIVE DrawingRevision per specific Drawing UUID**. It is enforced by a PostgreSQL partial unique constraint.
