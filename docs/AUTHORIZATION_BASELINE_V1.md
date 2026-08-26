# WP-002 Authorization Baseline v1

**Date:** 2026-08-26 — **scope:** accounts identity and domain-independent authorization contracts only.

## Evidence and classification

This baseline implements `DOMAIN_RULES_V1.md` AUTH-002–AUTH-007. Legacy evidence is `legacy/TeknikResimOlcum/Models/AppState.vb` (`NormalizeRole`, role constants/capabilities) and `legacy/TeknikResimOlcum/Services/AuthorizationService.vb` (`Require`). The legacy matrix named by the domain document is not present in this checkout; no absent semantics were inferred.

* **AS-IS confirmed:** the role/action grants and drawing scope rules below are the minimum matrix in AUTH-003–AUTH-006. `Yönetici` is broad read, not Admin/write. Backend enforcement is mandatory.
* **LEGACY-COMPAT:** `Kalite Kontrol Kullanıcısı` normalizes to canonical `plastic_quality` / “Plastikhane Kalite Kontrol”; it is not a second role. Canonical drawing scopes are `PLASTIC`, `INCOMING_QUALITY`, and `TR` (DRW-001).
* **TO-BE approved:** Authentik/OIDC, issuer+subject identity, no legacy credentials, explicit application Role/ActionPermission/ScopeGrant, and service-boundary enforcement (`SOURCE_OF_TRUTH_V1_1.md`, sections 5 and 10; AUTH-001/AUTH-007).
* **DEFERRED/unresolved:** OIDC group mapping, future object identifiers/handlers, ownership/row/field semantics, INO field matrix, and domain model queries. No global permission is inferred from these items.

## Canonical role catalog

`admin` Admin; `technical_drawing` Teknik Resim; `quality_manager` Kalite Kontrol Yöneticisi; `plastic_quality` Plastikhane Kalite Kontrol; `mechanism_quality` Mekanizma Kalite Kontrol; `mechanism_manager` Mekanizma Yöneticisi; `incoming_quality` Giriş Kalite Kontrol; `laboratory` Kalite Laboratuvar; `production_user` Üretim Kullanıcısı; `production_manager` Üretim Yöneticisi; `production_label` Üretim Etiket; `planning` Planlama; `manager` Yönetici. These are the constants accepted by `AppState.IsValidRole`. Business Admin remains separate from Django `is_superuser`.

## Action grants (AS-IS)

| Action | Roles |
|---|---|
| `drawings.manage` | Admin, Teknik Resim |
| `drawings.view` | drawing-scoped roles below |
| `measurements.create` | Admin, Kalite Kontrol Yöneticisi, Giriş Kalite, Plastikhane Kalite |
| `measurements.view_history` | preceding roles + Yönetici |
| `spc.view`; `msa.view` | Admin, Kalite Kontrol Yöneticisi, Yönetici |
| `spc.adjust_limits`; `msa.manage` | Admin only |
| `molding.bind` | Admin, Üretim Kullanıcısı, Üretim Yöneticisi |
| `molding.plan_manage` | Admin, Üretim Yöneticisi |
| `mold_tickets.delete`; `commissioning.delete` | Admin only |
| `commissioning.manage` | Admin, Üretim Yöneticisi, Kalite Kontrol Yöneticisi, Teknik Resim |
| `lab_requests.create` | Admin, Kalite Kontrol Yöneticisi, Mekanizma/Giriş/Plastikhane Kalite |
| `lab_requests.process` | Admin, Kalite Kontrol Yöneticisi, Kalite Laboratuvar |
| `lab_requests.skip_or_reopen_step` | Admin, Kalite Kontrol Yöneticisi |
| `package_meter.manage` | Admin, Kalite Kontrol Yöneticisi, Kalite Laboratuvar |
| `authorization_matrix.view` | Admin, Yönetici |

AUTH-006 remains a pure contextual rule: Mekanizma Kalite may process only when validated `requested_department == "MEKANİZMA"`; it is deliberately not granted global `lab_requests.process`.

## Drawing scope policy (AS-IS)

Admin, Teknik Resim and Kalite Kontrol Yöneticisi have all three scopes. Yönetici has all scopes for `drawings.view` only. Giriş Kalite has only `INCOMING_QUALITY`; Plastikhane Kalite and Üretim Etiket only `PLASTIC`; all others default to none. An action grant and matching active scope grant are both required; a global action never silently satisfies a scoped check.
