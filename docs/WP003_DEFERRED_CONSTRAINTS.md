# WP-003 Deferred Product/Mold Constraints

**Date:** 2026-08-26
**Gate:** An authoritative/current WP-000 profile has not been executed and approved.

These items are comparison and identity questions, not approved normalization rules. UUIDs are the only technical identities in WP-003. Original business values remain unchanged, and no uniqueness constraint below is encoded in the database or services.

## Product TR identity

`Product.tr_code` is an indexed business identifier. Whether one raw or comparison-normalized TR identifies exactly one Product is not established. Duplicate values are accepted.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## MoldCode identity

`Mold.mold_code` is an indexed business identifier. Legacy MoldCode can contain variants or multiple tokens, so neither raw nor tokenized uniqueness is approved. Duplicate values are accepted.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Product-Mold cardinality

Product and Mold are connected through an explicit many-to-many relation. Neither one-to-one ownership nor uniqueness of `(product, mold)` is assumed. Multiple links, including repeated pairs, cannot be rejected until profiling and ADR-012 review establish safe temporal/cardinality semantics.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Case sensitivity

No uppercase/lowercase canonical form or case-insensitive identity rule is approved. Stored case is preserved.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Whitespace normalization

Services reject identifiers that are blank when checked with ordinary surrounding-whitespace validation, but they do not trim accepted values. No whitespace-normalized uniqueness or merge rule is approved.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Separator normalization

Hyphens, slashes, punctuation, and other separators are preserved. No removal, substitution, token merge, or separator-insensitive identity rule is approved.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Leading-zero handling

Leading zeros are preserved as business-significant text. No numeric coercion, zero removal, or equivalence rule is approved.

`STATUS = DEFERRED_PENDING_WP000_DATA_GATE`

## Gate closure

These statuses can change only after the controlled WP-000 run, sanitized evidence review, and an approved identity/cardinality decision. A later migration—not an edit to WP-003 history—must introduce any resulting canonical fields or constraints.
