# A Blok Kalite Kontrol — LEGACY MAPPING v1

**Tarih:** 26.08.2026  
**Üst doküman:** `WEB_DONUSUM_MASTER_PLANI_V1.md`  
**İş kuralı referansı:** `DOMAIN_RULES_V1.md`  
**Amaç:** Mevcut WinForms/CSV/dosya tabanlı verinin normalize PostgreSQL modeline güvenli, idempotent ve doğrulanabilir biçimde taşınması.

---

# 1. Migration prensibi

Yeni sisteme geçiş yolu:

```text
Legacy CSV + Drawings + Attachments
              │
              ▼
        READ-ONLY SNAPSHOT
              │
              ▼
       PostgreSQL STAGING
              │
              ▼
     Normalize / Validate
              │
              ▼
       DOMAIN TABLES
              │
              ▼
       Reconciliation
```

**Yapılmayacak:**

```text
CSV → legacy SQL Server Schema.sql → PostgreSQL
```

`Schema.sql` dönüşüm şeması olarak kullanılmayacaktır. Bunun nedeni mevcut SQL Server hazırlığında çok sayıda alanın `NVARCHAR` olması ve kaynak uygulamadaki bütün veri kaynaklarını kapsamamasıdır.

---

# 2. Veri sınıfları

| Sınıf | Açıklama | Migration davranışı |
|---|---|---|
| **A — Core Domain** | İşletme tarihçesi ve iş süreçleri | Tam migrate + reconcile |
| **B — Supporting Domain** | Recipient, catalog, assignment, audit gibi destek verisi | Migrate/normalize |
| **C — Ephemeral Runtime** | Session, process, lock, local draft | Migrate edilmez |
| **D — Binary/File** | Drawing, attachment, photo | Controlled copy/decrypt/hash + metadata |
| **E — Operational Journal** | Pending transaction, error log, critical journal | Cutover öncesi çöz; aktif domain’e migrate etme, gerekiyorsa archive |
| **F — UI/Deployment** | HTML dashboard asset, update package, launcher | Migrate edilmez; yeni UI/deploy ile değiştirilir |
| **G — Discovery Required** | Yapısı/authoritative niteliği cutover’da ayrıca doğrulanacak | Profiling sonrası karar |

---

# 3. Migration altyapı tabloları

Domain tablolardan bağımsız aşağıdaki migration tabloları önerilir.

## `legacy_import_run`

| Alan | Tip | Açıklama |
|---|---|---|
| id | uuid | Import run |
| source_snapshot_id | text | Snapshot/backup kimliği |
| started_at | timestamptz | Başlangıç |
| finished_at | timestamptz | Bitiş |
| status | text | RUNNING/SUCCEEDED/FAILED/PARTIAL |
| code_version | text | Migration script git SHA/version |
| source_root_hash | text | İsteğe bağlı snapshot manifest hash |
| stats | jsonb | Sayımlar |

## `legacy_key_map`

| Alan | Tip | Açıklama |
|---|---|---|
| id | uuid | PK |
| import_run_id | uuid FK | Run |
| source_name | text | `MeasurementRecords.csv` vb. |
| source_key | text | RecordId/TicketId/composite key |
| target_table | text | Hedef tablo |
| target_id | uuid | Hedef UUID |
| row_hash | text | Ham row deterministik hash |
| created_at | timestamptz | Mapping zamanı |

Unique önerisi: `(source_name, source_key, target_table)`.

## `legacy_reject`

Parse edilemeyen, referansı bulunamayan veya belirsiz kayıtlar sessizce atılmaz.

Alanlar:

- source_name
- source_row_number
- source_key
- raw_data jsonb
- reason_code
- reason_detail
- severity
- resolved_at/resolved_by

## `legacy_staging_row`

İsteğe bağlı generic staging:

- source_name
- row_no
- raw_data jsonb
- row_hash
- parsed_ok
- validation_errors jsonb

Büyük veri hacminde source-specific staging table da kullanılabilir.

---

# 4. Global dönüşüm kuralları

## MAP-G001 — Encoding ve trim

- CSV encoding otomatik/konfigüre tespit edilir; kaynak byte snapshot korunur.
- String alanlarda normal trim yapılır.
- Internal anlam taşıyabilecek açıklama alanlarında whitespace agresif biçimde normalize edilmez.
- Türkçe karakterler korunur.

## MAP-G002 — Boş değerler

Legacy `""`, whitespace ve gerçek null source’a göre normalize edilir. Numeric/date/bool hedefte boş string tutulmaz; `NULL` kullanılır.

## MAP-G003 — Boolean

Legacy örnekleri:

- `YES`, `EVET`, `1`, `TRUE` → true
- `NO`, `HAYIR`, `0`, `FALSE`, boş → false veya source-specific default

`IsCritical` için kaynak kuralı daha sıkıdır: yalnız açık `YES` true kabul edilir.

## MAP-G004 — Decimal

Türkçe/Invariant decimal varyantları güvenli parser ile denenir. Parse başarısızsa kayıt reject edilir veya ilgili nullable alan null bırakılır; ölçüm gibi kritik değerlerde sessiz null’a dönüşüm yapılmaz.

## MAP-G005 — Tarih

Legacy yaygın formatlar:

- `yyyy-MM-dd HH:mm:ss`
- `dd.MM.yyyy HH:mm:ss`
- tarih-only varyantları

Parse sonucu `Europe/Istanbul` local time kabul edilip `timestamptz`’e dönüştürülür. Ham değer staging’de kalır.

## MAP-G006 — Kullanıcı çözümleme

Legacy username → `auth_user/user_profile` eşleşmesi case-insensitive yapılır. AD’de artık olmayan kullanıcılar için:

- FK nullable olabilir,
- `actor_username_snapshot` korunur,
- migration placeholder login hesabı üretmez.

## MAP-G007 — ComputerName

Bilgisayar adı business relation değildir. Audit/session metadata olarak saklanır; master “computer” tablosu oluşturmak zorunlu değildir.

## MAP-G008 — ID üretimi

Legacy ID’ler target UUID yapılmaz. Target UUID ayrı üretilir; legacy ID mapping table’da saklanır.

## MAP-G009 — Duplicate source satırı

Aynı source key + aynı row hash → idempotent skip.  
Aynı source key + farklı row hash → conflict/reject; otomatik son satırı kazan yaklaşımı uygulanmaz.

## MAP-G010 — Enum normalize

Display Türkçe metinleri DB internal enum’dan ayrılır. Legacy string hedef enum’a deterministic map edilir; bilinmeyen değer reject/catalog-review listesine düşer.

---

# 5. Authoritative veri kaynakları envanteri

Aşağıdaki liste `Services/AppPaths.vb` ve modül servislerinden doğrulanmıştır.

| Legacy kaynak | Sınıf | Hedef | Karar |
|---|---:|---|---|
| `Users.csv` | B | accounts profile/role membership | Kullanıcı metadata/rol migrate; password hash/salt yok |
| `Products.csv` | A | products + drawings + revisions + product_mold | Normalize |
| `ControlPoints.csv` | A | control_point + control_point_version | Normalize/version preserve |
| `MeasurementGroupAreas.csv` | A | control_point_group_area | Migrate |
| `MeasurementRecords.csv` | A | inspection_session + eye + measurement | Migrate; RecordId güvenli sınır |
| `MeasurementCorrections.csv` | A | measurement_revision | Migrate |
| `SpcLimitCorrections.csv` | A | spc_limit_correction | Migrate |
| `VisualControlRecords.csv` | A | visual_control | RecordId ile inspection eye’a bağla |
| `ClosedEyeRecords.csv` | A | inspection_session + closed eye | Migrate |
| `AuditLog.csv` | B | audit_event | `source=LEGACY` |
| `CriticalDataJournal.csv` | E | archive / migration investigation | Aktif domain’e birebir taşıma yok |
| `ApplicationErrors.log` | E | log archive | DB business data değil |
| `ProductionTickets.csv` | A | production_ticket | Migrate |
| `MoldBindingRecords.csv` | A | molding_binding | Migrate |
| `MoldTickets.csv` | A | mold_ticket | Migrate |
| `QualityToProductionTickets.csv` | A | quality_production_ticket | Migrate |
| `MoldConnectionPlan.csv` | A | connection_plan_import + row | Migrate |
| `MoldConnectionPlanEmailRecipients.csv` | B | notification_recipient | Normalize |
| `NewMoldCommissionings.csv` | A | commissioning | Migrate |
| `NewMoldCommissioningChecklist.csv` | A | commissioning_checklist_item | Migrate |
| `NewMoldCommissioningTrials.csv` | A | commissioning_trial | Migrate |
| `NewMoldCommissioningActions.csv` | A | commissioning_action | Migrate |
| `MechanismQualityControlRecords.csv` | A | mechanism_quality_delivery | Migrate |
| `PlasticShiftTrackingRecords.csv` | A | shift_record(module=PLASTIC) | Migrate |
| `MechanismShiftTrackingRecords.csv` | A | shift_record(module=MECHANISM) | Migrate |
| `ShiftTrackingPhotos.csv` | B/D | shift_photo + file_object | Index + binary copy |
| `PlasticShiftErrorReports.csv` | A | nonconformity_report + action + review | Flat→normalized |
| `PlasticShiftErrorReportEvaluatorAssignments.csv` | B | evaluator_assignment | Migrate |
| `PlasticShiftErrorReportEvaluations.csv` | A | nonconformity_evaluation | Migrate |
| `PlasticShiftErrorReportEmailEvents.csv` | B | notification_event | Migrate/idempotency history |
| `PlasticShiftEmailRecipients.csv` | B | notification_recipient | Normalize |
| `MechanismShiftEmailRecipients.csv` | B | notification_recipient | Normalize |
| `MechanismQualityEmailRecipients.csv` | B | notification_recipient | Normalize |
| `TestRequestRecords.csv` | A | laboratory_test_request | Migrate |
| `TestRequestSteps.csv` | A | laboratory_test_request_step | Migrate/snapshot |
| `TestCatalog.csv` | B | laboratory_test_catalog | Migrate |
| `TestGroups.csv` | B | test_group + group_item | Parse/normalize |
| `TestRequestAttachments.csv` | B/D | test_request_attachment + file_object | Index + binary copy |
| `TestRequestEmailRecipients.csv` | B | notification_recipient | Normalize with event/dept scope |
| `TestRequestEmailEvents.csv` | B | notification_event | Migrate |
| `MeasurementDevices.csv` | A/B | metrology_device | Migrate |
| `PackageMeterControls.csv` | A | package_meter_control | Migrate |
| `PackageMeterControlLines.csv` | A | package_meter_line | Migrate |
| `PackageMeterEmailRecipients.csv` | B | notification_recipient | Normalize |
| `INO_Database.csv` | A | ino_record | Migrate; canlı INO kaynağı |
| `INO_Database.seed.csv` | F/B | ino seed/reference | Canlı CSV yoksa bootstrap referansı; historical source gibi ayrıca migrate etme |
| `ActiveSessions.csv` | C | — | Migrate etme |
| `SessionEndRequests.csv` | C | — | Migrate etme |
| `RunningInstances.csv` | C | — | Migrate etme |
| `MeasurementDrafts/` | C | — | Migrate etme |
| `PendingTransactions/*.json` | E | — | Cutover öncesi recovery tamamla; sonra archive |
| `TransactionRecovery.log` varsa | E | — | Archive |
| `Drawings/**` | D | drawing_revision.file_object | Decrypt/copy/hash |
| `ShiftTrackingPhotos/**` | D | file_object | Copy/hash |
| `TestRequestAttachments/**` | D | file_object | Copy/hash |
| `ScrapDashboardState.json` | G | analytics import/config | Ayrı profil; core migration blocker değil |
| `ReworkDashboardState.json` | G | analytics import/config | Ayrı profil; core migration blocker değil |
| `YETKI_MATRISI.csv` / `.md` | B/F | role-permission seed/reference | İşlem geçmişi değil; permission bootstrap ve test oracle olarak kullan |
| `Resources/*.html` | F | yeni Django/Chart.js UI | Veri olarak migrate etme |
| `Updates/`, `Versions/`, launcher | F | Docker/CI-CD | Migrate etme |
| `UserStoreBackups/` | C/E | archive | Yeni auth store’a import etme |

---

# 6. Users.csv → Accounts

Legacy headers:

`Username, PasswordHash, PasswordSalt, Role, IsActive, ShowOnLogin, IsPermissionTestAccount, MustChangePassword, PasswordChangedAt, CreatedAt, LastLoginAt`

## Mapping

| Legacy | Hedef | Dönüşüm |
|---|---|---|
| Username | user_profile.external_username + username snapshot | trim, case-insensitive identity |
| PasswordHash | — | **MIGRATE ETME** |
| PasswordSalt | — | **MIGRATE ETME** |
| Role | role membership | normalize legacy role |
| IsActive | user_profile.is_active | bool |
| ShowOnLogin | legacy metadata / drop | OIDC login’de anlamı yok; gerekiyorsa json metadata |
| IsPermissionTestAccount | user_profile.is_permission_test_account | bool; yalnız test amacı |
| MustChangePassword | — | OIDC/AD’ye bırak |
| PasswordChangedAt | — | credential history migrate edilmez |
| CreatedAt | user_profile.legacy_created_at | parse |
| LastLoginAt | user_profile.legacy_last_login_at | parse |

### Cutover davranışı

- AD/OIDC’de eşleşen kullanıcıya profile bağlanır.
- Eşleşmeyen legacy kullanıcı tarihsel actor olarak korunur; aktif login hesabı yaratılmaz.
- Role membership ilk migration’da legacy role’den bootstrap edilir; sonrasında yeni permission source of truth web DB/AD group mapping olur.

---

# 7. Products.csv → Product / Drawing / Revision / Mold

Legacy headers:

`TrCode, ProductName, PlasticCode, Material, ColorName, MoldCavityCount, MoldCode, DrawingRev, DrawingFile, DrawingScope, IsActive, CreatedBy, CreatedAt`

## 7.1 Product

| Legacy | Hedef |
|---|---|
| ProductName | product.name |
| PlasticCode | product.plastic_code |
| Material | product.material_text veya catalog FK |
| ColorName | product.color_name |
| IsActive | product.is_active — aggregation kuralı ile |
| CreatedBy/At | historical audit/snapshot |

**Kimlik problemi:** Legacy product satırında ayrı ProductId yoktur. Aynı product birden çok drawing revision satırında tekrar edebilir.

### Product grouping v1

Önce veri profili çıkarılır. İlk grouping candidate:

`normalize(ProductName) + normalize(PlasticCode)`

Ancak otomatik merge yalnız metadata uyumluysa yapılır. Conflict varsa ayrı staging cluster ve manual resolution.

## 7.2 Mold

`MoldCode` ham string ayrıca `legacy_mold_code_text` olarak saklanır. `SplitMoldCodeTokens` mantığıyla deterministik token’lar `products_mold` tablosuna, ilişkiler `product_mold` tablosuna yazılır.

`MoldCavityCount` numeric parse edilebiliyorsa mold/product relation metadata’da cavity_count’a yazılır; conflict’ler raporlanır.

## 7.3 Drawing

| Legacy | Hedef |
|---|---|
| TrCode | drawing.tr_code |
| DrawingScope | drawing.scope enum |
| Product cluster | drawing.product_id |

**ERD v1 provisional identity:** `normalized(tr_code) + scope` tek drawing kabul edilir. Migration profiling bu varsayımı mutlaka doğrulamalıdır. Aynı TR+scope birden çok gerçek product’a çakışıyorsa constraint uygulanmadan ADR açılır.

## 7.4 Revision

| Legacy | Hedef |
|---|---|
| DrawingRev | drawing_revision.revision_code |
| DrawingFile | file_object + drawing_revision.file_id |
| IsActive | revision status hesabı |
| CreatedBy/At | created snapshot/FK |

Aynı drawing altında birden çok active legacy row varsa otomatik keyfi seçim yapılmaz; aktiflik conflict report’a düşer. Kullanıcı onayıyla tek ACTIVE seçilir veya cutover policy belirlenir.

---

# 8. Drawings/ binary migration

## Kaynak güvenlik

Legacy `DrawingFile` yalnız Drawings root ve en fazla bir scope klasörü altında resolve edilir. Migration da aynı path traversal kontrollerini uygular.

## İşlem

Her referanslı drawing file için:

1. safe legacy full path resolve,
2. existence kontrol,
3. `.enc` ise legacy AES-GCM decrypt,
4. plaintext/target bytes integrity kontrol,
5. SHA-256 üret,
6. MIME tespit,
7. target storage key üret,
8. binary atomic copy/upload,
9. `core_file_object` kaydı,
10. drawing revision link,
11. legacy→target mapping.

## Hatalar

- dosya yok → revision kaydı `FILE_MISSING` migration issue ile oluşturulabilir; ACTIVE yapılmadan manual resolution.
- decrypt fail → hard reject / blocker.
- aynı filename farklı hash → farklı file objects.
- aynı hash farklı legacy path → dedup opsiyoneldir; ilk migration’da dedup zorunlu değil.

**Legacy encryption key target storage’a kopyalanmaz.**

---

# 9. ControlPoints.csv → Logical Control Point + Version

Legacy headers:

`TrCode, DrawingRev, DrawingScope, MeasureId, MeasureName, Nominal, LowerTol, UpperTol, LowerLimit, UpperLimit, PageNo, XPercent, YPercent, Unit, IsMandatory, MeasurementGroup, SampleFrequency, IsCritical, SortNo, IsActive, SpcKey, MeasureVersion, ValidFrom, ValidTo, ChangeReason`

## 9.1 Revision resolution

`TrCode + normalized scope + DrawingRev` → drawing_revision.

Bulunamazsa control point import edilmez; `ORPHAN_DRAWING_REVISION` reject.

## 9.2 Logical control point grouping

Primary logical grouping:

`drawing_id + normalized SpcKey`

`SpcKey` boşsa legacy `MeasureId` kullanılır.

Aynı SPC key’nin gerçekte farklı ölçüleri temsil ettiği conflict veri profiler ile kontrol edilmelidir.

## 9.3 Version mapping

| Legacy | Hedef |
|---|---|
| MeasureVersion | control_point_version.version_no, min 1 |
| MeasureId | measure_code |
| MeasureName | measure_name |
| Nominal | numeric |
| LowerTol | `-abs(value)` semantic |
| UpperTol | `abs(value)` |
| LowerLimit | validation reference; canonical limit yeniden hesaplanır |
| UpperLimit | validation reference; canonical limit yeniden hesaplanır |
| PageNo | page_no >= 1 |
| XPercent/YPercent | numeric 0..100 |
| Unit | default mm |
| IsMandatory | bool default true |
| MeasurementGroup | default Genel |
| SampleFrequency | default Her Kontrol |
| IsCritical | exact YES → true |
| SortNo | integer |
| IsActive | version active flag / validity |
| ValidFrom/To | timestamptz/date as semantics dictate |
| ChangeReason | change_reason |

### Limit discrepancy

Legacy CSV LowerLimit/UpperLimit ile nominal±tol hesabı farklıysa:

- calculated value canonical kabul edilir çünkü runtime code bunu kullanır,
- discrepancy `legacy_reject` değil `WARNING` reconciliation kaydı olur,
- original columns staging’de korunur.

---

# 10. MeasurementGroupAreas.csv

`TrCode + Rev + Scope` → drawing_revision.

| Legacy | Hedef |
|---|---|
| GroupName | group_name |
| PageNo | page_no |
| Left/Top/Right/BottomPercent | numeric rectangle |
| UpdatedBy/At | audit metadata |

Validation:

- 0..100,
- right > left,
- bottom > top,
- page >= 1.

Geometrik invalid kayıt manual review/reject.

---

# 11. MeasurementRecords.csv → Inspection model

Legacy headers:

`RecordId, TrCode, DrawingRev, DrawingScope, LotNo, SerialNo, EyeCount, EyeNo, OperatorName, ComputerName, MeasurementDate, MeasureId, MeasureName, MeasurementGroup, SampleFrequency, IsCritical, SortNo, Nominal, LowerLimit, UpperLimit, PageNo, XPercent, YPercent, MeasuredValue, Result, Note, ProductionTicketId, SpcKey, MeasureVersion, CommissioningId`

## 11.1 En önemli migration kararı

**Bir legacy RecordId = bir target inspection session** olarak migrate edilir.

Sebep: legacy “tüm gözleri kaydet” işlemi her göz için ayrı RecordId üretir. Parent multi-eye session kimliği yoktur. Tarih/operatör/lot benzerliğine göre yapay merge veri bütünlüğü riski yaratır.

Future web kayıtları bir session altında çok göz kullanabilir; legacy import buna engel değildir.

## 11.2 Session

RecordId bazında distinct header alanları alınır:

| Legacy | Hedef |
|---|---|
| RecordId | inspection_session.legacy_record_id + key map |
| TR/Rev/Scope | drawing_revision_id |
| LotNo | lot_no |
| SerialNo | serial_no |
| EyeCount | declared_eye_count |
| OperatorName | operator snapshot/user FK |
| ComputerName | source_computer_name |
| MeasurementDate | measured_at/session completed_at |
| ProductionTicketId | production_ticket FK resolution |
| CommissioningId | commissioning FK resolution |

Aynı RecordId içindeki header alanları birbirinden farklıysa conflict reject.

## 11.3 Eye

Bir RecordId normalde tek EyeNo temsil eder.

- `eye_no` = legacy EyeNo
- `session_id` = mapped RecordId
- unique `(session_id, eye_no)`

## 11.4 Measurement

Her row bir measurement.

| Legacy | Hedef |
|---|---|
| MeasureId + SpcKey + MeasureVersion | control_point_version resolution; fallback snapshot-only |
| MeasuredValue | measured_value numeric |
| Result | legacy_result + recalculated verification |
| Note | note |
| MeasurementDate | measured_at |
| all nominal/limit/name/group/etc. | snapshot fields |

### Control point FK resolution order

1. drawing revision + spc_key + version_no
2. drawing revision + measure_id + version_no
3. drawing revision + measure_id
4. eşleşme yoksa measurement korunur fakat `control_point_version_id=NULL`, snapshot authoritative olur ve migration warning üretilir.

### Result reconciliation

Numeric value ve snapshot limits parse edilebiliyorsa result server-side yeniden hesaplanır.

- eşitse → OK
- farklıysa target `result` canonical hesap; `legacy_result` ayrıca saklanır veya reconciliation warning.

Legacy historical sonucu sessizce kaybetmemek için warning raporlanır.

---

# 12. ClosedEyeRecords.csv

Legacy fields:

`RecordId, TrCode, DrawingRev, DrawingScope, LotNo, SerialNo, EyeCount, EyeNo, OperatorName, ComputerName, ClosedDate, Reason, ProductionTicketId, CommissioningId`

Her RecordId için measurement’ta yoksa ayrı inspection session oluşturulur. Eye:

- `is_closed=true`
- closed_at
- close_reason
- no measurements
- no visual controls

Aynı RecordId MeasurementRecords’da da varsa conflict review gerekir; normal kaynak akışında olmaması beklenir.

---

# 13. VisualControlRecords.csv

RecordId + EyeNo üzerinden inspection eye bulunur.

| Legacy | Hedef |
|---|---|
| ControlName | control_name |
| IsSelected | is_selected bool |
| Result | result enum/text |
| Note | note |
| ControlDate | controlled_at |
| OperatorName | actor snapshot |

RecordId eşleşmezse orphan reject.

---

# 14. MeasurementCorrections.csv

Legacy:

`CorrectionId, RecordId, TrCode, DrawingRev, EyeNo, MeasureId, MeasureName, MeasurementDate, OldValue, NewValue, OldResult, NewResult, Reason, ChangedBy, ChangedAt, ComputerName`

Resolution:

1. RecordId → session
2. EyeNo → eye
3. MeasureId (+ MeasurementDate gerektiğinde) → measurement

Hedef `measurement_revision`:

- legacy_correction_id
- measurement_id
- old/new value
- old/new result
- reason
- changed_by snapshot/FK
- changed_at
- source_computer_name

Correction sırası ChangedAt + source order ile korunur. Final measurement current value correction zinciriyle uyuşmuyorsa warning.

---

# 15. SpcLimitCorrections.csv

Map:

- TrCode/Rev/Scope → drawing revision
- SpcKey → logical control point
- DateFrom/To → effective range
- OldLimits → raw_old_limits text/json
- NewNominal/lower/upper → numeric
- AffectedRows/ResultChangedRows → integer historical stats
- reason/actor/time/computer

Bu kayıtlar migration sırasında **tekrar measurement satırlarına uygulanmaz**; MeasurementRecords zaten legacy sistemin son durum snapshot’ını içermelidir. Aksi yapılırsa correction ikinci kez uygulanabilir.

---

# 16. ProductionTickets.csv

Legacy headers:

`TicketId, Status, CreatedAt, CreatedBy, ComputerName, MachineNo, PreviousMachineNo, MoldCode, TrCode, DrawingRev, ProductName, Material, ColorName, PlasticCode, RawMaterial, WorkOrderNo, Note, SeenByQuality, SeenAt, ClosedBy, ClosedAt, CloseNote, BindingId, BindingStartAt, BindingEndAt, BindingDurationMin, BindingReason, MachineChangeReason`

Target `tickets_production_ticket`.

Mapping:

- TicketId → legacy_ticket_id
- Status normalize OPEN/SEEN/CLOSED
- BindingId → molding_binding FK (deferred FK pass)
- TR/rev snapshot korunur; drawing revision FK bulunabiliyorsa bağlanır
- MoldCode raw + parsed mold FK mümkünse
- product/material/color/plastic code snapshots korunur
- actor/time fields parse
- duration integer numeric

**Naming notu:** Bu legacy tablo kalıp bağlama sonrası kaliteye düşen “production ticket” semantiği taşır. UI isimleri yeniden adlandırılsa bile migration source adı korunur.

---

# 17. MoldBindingRecords.csv

Legacy headers:

`BindingId, Status, StartedAt, StartedBy, StartComputerName, CompletedAt, CompletedBy, CompletedComputerName, MachineNo, PreviousMachineNo, MoldCode, TrCode, DrawingRev, ProductName, Material, ColorName, PlasticCode, RawMaterial, WorkOrderNo, BindingReason, MachineChangeReason, StartNote, FinishNote, Note, BindingDurationMin, ProductionTicketId`

Target `molding_binding`.

Key transforms:

- STARTED/COMPLETED enum
- machine no text/canonical machine FK if catalog exists
- MoldCode raw + single canonical mold only if deterministic; multiple token ilişkisi gerekirse child `binding_mold`
- TR/rev → drawing revision
- linked production ticket deferred pass
- notes ayrı alanlara korunur

### Duplicate STARTED

Legacy’de aynı mold için birden çok STARTED olabilir. Eğer TO-BE hard uniqueness onaylanırsa migration öncesi bu kayıtlar **otomatik kapatılmaz/silinmez**. Cutover exception listesi çıkarılır ve iş sahibi hangi kayıtların aktif kalacağını seçer.

---

# 18. MoldTickets.csv

Legacy:

`MoldTicketId, Status, CreatedAt, CreatedBy, ComputerName, MoldCode, TrCode, DrawingRev, ProductName, Severity, ProblemType, ProblemDescription, ActionPlan, SourcePlasticShiftRecordId, ClosedBy, ClosedAt, CloseNote`

Target:

- `mold_ticket`
- source shift FK
- drawing revision FK mümkünse
- mold resolution MoldCode token ile
- severity/problem type enum/catalog’a normalize
- raw text korunur

SourcePlasticShiftRecordId bulunamazsa ticket yine historical olarak korunabilir; FK nullable + orphan warning.

---

# 19. QualityToProductionTickets.csv

Legacy headers:

`TicketId, Status, CreatedAt, CreatedBy, ComputerName, TrCode, DrawingRev, ProductName, LotNo, SerialNo, EyeCount, EyeNo, RecordId, SourceQualityTicketId, SourceType, IssueSummary, MeasurementNokCount, VisualNokCount, SeenByProduction, SeenAt, ClosedBy, ClosedAt, CloseNote`

Target `quality_production_ticket`.

- RecordId → inspection session
- SourceQualityTicketId → source production ticket FK mümkünse
- one-per-RecordId uniqueness profil edilir ve hedef constraint uygulanır
- NOK counts integer
- TR/rev/lot/serial/eye snapshots korunur

Duplicate RecordId ticket varsa conflict/manual resolution; otomatik biri silinmez.

---

# 20. MoldConnectionPlan.csv

Legacy row alanları import provenance + machine + current/first/second mold bilgilerini içerir.

## Parent import grouping

`ImportedAt + ImportedBy + SourceFile + SourceSheet` kombinasyonu parent `connection_plan_import` için candidate group’tur. `PlanId` her row’da unique ise row legacy key olarak korunur.

## Row mapping

- SourceRow integer
- machine name/no
- running molds raw
- current mold/rack/plastic/TR
- first mold/rack/plastic/TR
- second mold/rack/plastic/TR

İlk sürüm target row bunları typed/text kolonlarla korur. Sonraki revizyonda sequence child table’a dönüşebilir.

---

# 21. New Mold Commissioning

## 21.1 Parent — NewMoldCommissionings.csv

Ana alanlar:

- identity/status/current stage/audit
- product/drawing/mold
- manufacturer/cavity/material/color/masterbatch
- planned machine/target cycle/quantity
- critical dimensions/special characteristics/function tests/mating parts/customer requirements
- requested production date/departments/documents note
- Mechanical/Product/Process approval triple actor/time
- FinalDecision/final note
- conditional until/quantity
- next trial date

### Target normalization

`commissioning` parent:

- request/product/mold fields
- legacy_current_stage snapshot
- computed current_stage
- status
- planning fields
- final decision fields

`commissioning_approval` child:

- approval_type = MECHANICAL/PRODUCT/PROCESS
- decision
- decided_by/at

FinalDecision parent veya `approval_type=FINAL` şeklinde tutulabilir. ERD v1 final decision’ı ayrıca modellendirir çünkü conditional fields buna bağlıdır.

## 21.2 Checklist

`ChecklistId, CommissioningId, ItemNo, Category, ItemText, Result, Explanation, CheckedBy, CheckedAt`

→ `commissioning_checklist_item`.

## 21.3 Trial

Her TrialId child row. Numeric alanlar parse edilebildiği kadar typed; parse edilemeyen legacy values reject/warning policy ile ele alınır.

## 21.4 Action

Her ActionId child row. TrialNo ilişkisi string/no olabilir; mümkünse matching trial FK, değilse legacy_trial_no snapshot.

### Status reconciliation

Legacy parent Status, `AllApprovalsComplete` kuralıyla yeniden doğrulanır. `ŞARTLI ONAY` olan fakat `TAMAMLANDI` yazan anomali hard warning.

---

# 22. Shift Tracking

Plastic ve Mechanism CSV aynı field setine map edilir:

`shift_record.module_type = PLASTIC | MECHANISM`

Legacy:

`RecordId, OccurredAt, DefectiveQuantity, Responsible, ProductNameCode, Problem, ActionTaken, YellowCard, MoldModification, ErrorReport, TestPerformed, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, ComputerName`

Mapping:

- RecordId → legacy_record_id
- DefectiveQuantity → `defective_quantity_text`
- numeric parse başarıysa optional numeric field
- YES/NO flags boolean
- audit actor/time

Plastic ve mechanism RecordId alanları global unique olmayabilir; legacy key `(source_file, RecordId)` olmalıdır.

---

# 23. ShiftTrackingPhotos.csv + binary

Index headers:

`PhotoId, RecordId, ModuleType, RelativePath, OriginalFileName, AddedBy, AddedAt, ComputerName`

Process:

1. ModuleType normalize
2. source shift record resolve source-aware
3. relative path security check
4. file exists + size/hash/mime
5. target file object
6. shift_photo row

Legacy allowed image extensions: jpg/jpeg/png/bmp/gif. Web hedefinde gerçek image MIME validation önerilir.

---

# 24. PlasticShiftErrorReports.csv → normalized NCR

Legacy flat tablo çok geniştir.

## 24.1 Parent `nonconformity_report`

Parent’a taşınacak alanlar:

- ReportId / ReportNo
- ShiftRecordId
- Status / RevisionDate
- source department / quality control point
- part/product/TR/type/quantity/machine/operator
- defect area/code/type
- nonconformity description
- quality inspector/detected by
- unit manager approval
- disposition
- kaizen responsible/no
- root cause
- verification fields
- close approved/note
- created/updated actor/time/computer

## 24.2 Action1..Action5

Her dolu legacy action slot → bir `nonconformity_action`.

Alanlar:

- slot_no
- action_text
- responsible
- due_date
- closed_date

Boş slot import edilmez.

## 24.3 Review çiftleri

Aşağıdaki legacy çiftler:

- StockReviewResult / Detail
- AffectedProcessResult / Detail
- AffectedProductResult / Detail
- DocumentNeedResult / Detail
- DrawingRevisionResult / Detail
- MoldRevisionResult / Detail
- SemiFinishedReviewResult / Detail

→ child `nonconformity_review_item`:

- review_type enum
- result
- detail

## 24.4 Evaluations

Ayrı evaluation CSV ile bağlanır. Parent’ın legacy status’ı evaluator state ile reconciliation edilir; mismatch raporlanır ama historical status silinmez.

---

# 25. Evaluator assignments/evaluations

## Assignment CSV

`PositionKey, PositionName, RequiredRole, UserName, Email, IsActive, UpdatedBy, UpdatedAt`

→ `nonconformity_evaluator_assignment`.

PositionKey canonical key’dir.

## Evaluation CSV

`EvaluationId, ReportId, PositionKey, PositionName, RequiredRole, AssignedUserName, AssignedEmail, Decision, Explanation, EvaluatedBy, EvaluatedAt, UpdatedAt, ComputerName`

→ `nonconformity_evaluation`.

Assignment snapshot alanları evaluation’da korunur. Bugünkü assignment değişse bile geçmiş değerlendirme değişmez.

---

# 26. Email events / recipients → Notifications

## Generic recipient target

Farklı CSV’ler tek modelde normalize edilir:

- module
- event_type nullable
- requesting_department nullable
- email
- display_name
- recipient_type nullable
- is_active
- audit fields

Kaynak dosya `legacy_source` olarak korunur.

## Event target

Legacy `EventKey`, ReportId/RequestId, EventType, SentAt, SentBy, ComputerName, Recipients → `notification_event`.

- event_key unique/idempotency key
- entity type/id resolution
- recipients historical text/json snapshot

Recipient listesini yeniden resolve edip geçmiş mail alıcısını değiştirmeyin.

---

# 27. MechanismQualityControlRecords.csv

Legacy:

`ControlId, Status, CreatedAt, ControlDateTime, IncomingEyeCount, DeliveredBy, ProductNameCode, MountedMechanismCounter, Explanation, DeliveryExplanation, ControlExplanation, IsSuitable, IsNotSuitable, ControlledBy, ControlledAt, CreatedComputerName, ControlledComputerName`

Target `mechanism_quality_delivery`.

Rules:

- ControlId legacy key
- incoming eye integer >=1
- status normalize PENDING/completed equivalent
- IsSuitable/IsNotSuitable boolean reconciliation: ikisi aynı anda true olamaz
- legacy `Explanation` semantic fallback: pending → delivery explanation; completed → control explanation
- product name/code raw snapshot; mümkünse product FK

---

# 28. Test Request Records

Legacy headers:

`RequestId, Status, CreatedAt, CreatedBy, CreatedComputerName, RequestingDepartment, RequestedDepartment, RequestReason, ProductNameTrCode, RequestedTests, SampleQuantity, Priority, DueDate, RequesterReportNo, RequesterExplanation, AcceptedAt, AcceptedBy, CompletedAt, CompletedBy, LabReportNo, Result, LabExplanation, CancelledAt, CancelledBy, CancelReason, UpdatedAt, UpdatedBy`

Target `laboratory_test_request`.

Mapping:

- RequestId legacy key
- status OPEN/ACCEPTED/COMPLETED/CANCELLED
- department text → department catalog FK + snapshot
- RequestReason → `reason_summary` raw; deterministic structured reasons varsa child rows
- ProductNameTrCode raw snapshot + best-effort product/drawing resolution
- RequestedTests raw snapshot korunur
- SampleQuantity typed integer yalnız parse edilebiliyorsa; raw fallback gerekirse
- Priority enum/catalog
- due date
- lifecycle actor/time fields

**RequestedTests alanı source of truth olarak step bulunan taleplerde kullanılmaz; step CSV authoritative snapshot’tır.**

---

# 29. TestRequestSteps.csv

Legacy headers:

`RequestId, StepId, SortNo, TestName, TestDescription, Status, Result, Explanation, CompletedAt, CompletedBy, CompletedComputerName, SkippedAt, SkippedBy, SkipReason, ReopenedAt, ReopenedBy, ReopenReason, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy`

Target `laboratory_test_request_step`.

- RequestId FK zorunlu
- StepId legacy key request scope’unda unique
- sort no positive
- status normalize PENDING/COMPLETED/SKIPPED
- catalog FK best-effort; test name/description snapshot authoritative
- complete/skip/reopen metadata korunur

Request status ile step state contradiction reconciliation report’a girer.

---

# 30. TestCatalog.csv / TestGroups.csv

## Catalog

- TestName → unique canonical display name
- Description
- IsActive
- SortNo
- audit

Case-insensitive duplicate test name conflict report edilir.

## Groups

Legacy `TestsText` içindeki test listesi source formatına göre parser ile ayrılır. Parser deterministic değilse raw text korunur ve manual review gerekir.

Target:

- test_group
- test_group_item(test_catalog_id, sort_no)

Unknown test name için sessiz yeni catalog kaydı yaratmayın; manual resolution veya explicit import policy.

---

# 31. TestRequestAttachments.csv + binary

Index:

`AttachmentId, RequestId, RelativePath, OriginalFileName, FileSize, AddedBy, AddedAt, ComputerName`

Process:

- request resolve
- safe path resolve under `Data/TestRequestAttachments`
- max 50 MB legacy kuralı doğrula
- allowed extension legacy listesi
- file exists/actual size/hash/mime
- `file_object`
- `test_request_attachment`

Index FileSize ile actual size farklıysa warning; actual file size canonical.

---

# 32. MeasurementDevices.csv

Legacy alan seti:

`DeviceId, FixedAssetNo, StdIso9001, StdIso45001, StdIso50001, StdIso46001, StdIso17020, StdIso17025, DeviceName, SerialNo, Brand, Model, DeviceType, MeasurementRange, Resolution, Unit, Location, ReferenceDevice, UsageStatus, RegistrationDate, Note, Status, CalibrationPeriodMonths, CalibrationDate, CalibrationDueDate, Organization, Responsible, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt`

Target `metrology_device`:

- internal uuid
- legacy device id + human device_code
- fixed asset no
- six ISO bool flags
- name/serial/brand/model/type
- range/resolution text veya typed ek alan
- unit/location/reference device
- usage/status enums
- registration date
- note
- calibration period int
- calibration/due date
- organization/responsible
- audit

ReferenceDevice self-FK yalnız deterministic unique DeviceId/name eşleşmesi varsa kurulur; raw reference text korunur.

---

# 33. PackageMeterControls.csv

Target `package_meter_control`.

Önemli mapping:

- ControlId legacy key
- status DRAFT/COMPLETED
- MeterModel, PulseCount, Customer
- ControlDate
- operator/controller snapshots
- production/control panel no
- IsSmartMeter bool
- Q4/Q3/Q2/Q1 numeric
- RangeValue integer enum/check
- Explanation
- counts integers
- lifecycle actor/time

Completion sonucu migration sırasında tekrar state transition olarak tetiklenmez; historical completed state import edilir ve validation/reconciliation yapılır.

---

# 34. PackageMeterControlLines.csv

Legacy:

`ControlId, LineId, SortNo, SerialNumber, LabelErrorQ3, LabelErrorQ2, LabelErrorQ1, TestFlowQ4Manual, TestFlowQ3, TestFlowQ2, TestFlowQ1, CreditResult, ValveResult, OverallResult, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy`

Target `package_meter_line`.

- ControlId FK
- LineId legacy key control scope’unda
- sort
- serial
- numeric error/test fields typed
- credit/valve result enum/text
- overall legacy result + optional recalculated check

Duplicate serial within completed control hard migration warning/blocker.

---

# 35. INO_Database.csv

Seed/source kolonları:

- SIRA NO
- SAYAÇ ADI
- SİPARİŞ YERİ
- İŞ EMRİ NO
- INO 1 VERİLEN BÖLÜM
- İNO-1 ONAY TARİHİ
- İNO-1 ONAYI VEREN
- İNO-1 RAPOR NO
- INO 1 DURUMU
- İNO-2 ONAY TARİHİ
- İNO-2 ONAYI VEREN
- İNO 2 RAPOR NO
- Q4
- Q3
- ARA DEBİ
- Q2
- Q1
- TAM (+)
- TAM (-) (2)
- INO 2 DURUMU
- AÇIKLAMA
- INO TALEP TARİHİ

Target `ino_record` typed/text fields.

### Stable row identity

`SIRA NO` tekil ve stabil ise legacy key olarak kullanılır. Duplicate/boş ise source row hash + generated legacy key; UI `__APP_ROW_ID` kalıcı business identity sayılmaz.

### Numeric flow fields

Q4/Q3/Ara/Q2/Q1/TAM değerleri numeric parse edilebildiği kadar numeric; parse edilemeyen nonempty values manual review. Ham JSON staging’de kalır.

---

# 36. AuditLog.csv

Legacy headers:

`LogId, DateTime, UserName, Role, ComputerName, Action, TrCode, DrawingRev, Detail`

→ `audit_event`:

- source=`LEGACY`
- legacy_log_id
- occurred_at
- actor snapshots/FK
- source_computer
- action
- entity hints TR/rev
- detail raw text
- before/after null

Legacy audit log target domain FK’ye kesin bağlanamıyorsa raw entity hints yeterlidir; yanlış FK kurmayın.

---

# 37. Ephemeral/runtime veriler — migrate edilmeyecek

## ActiveSessions.csv

Yeni web session mekanizmasıyla anlamını kaybeder. Tarihsel audit ihtiyacı varsa yalnız cutover snapshot archive edilir.

## SessionEndRequests.csv

Desktop multi-instance/session termination komut kuyruğudur. Web auth/session sistemiyle değiştirilir.

## RunningInstances.csv

WinForms process/PC instance heartbeat verisidir. Migrate edilmez.

## MeasurementDrafts/

Kullanıcı LocalAppData draftları authoritative business record değildir. Cutover öncesi kullanıcıların açık taslakları tamamlaması/iptal etmesi gerekir.

## Lock files (`INO_Database.lock`, `.lockinfo` vb.)

Migrate edilmez.

---

# 38. Journal ve recovery verileri

## PendingTransactions/

Cutover anında klasör boş olmalıdır. Boş değilse legacy uygulama recovery prosedürü çalıştırılır; ilişkili CSV’lerde transaction’ın gerçekten tamamlanıp tamamlanmadığı doğrulanır.

Pending journal doğrudan PostgreSQL domain kaydına dönüştürülmez.

## CriticalDataJournal.csv

Öncelikle hangi incomplete/recovery olaylarını tuttuğu profile edilir. Business record’a dönüşmedikçe archive olarak saklanır.

## ApplicationErrors.log

Central logging sistemine historical archive olarak taşınabilir; PostgreSQL business tabloya import edilmez.

---

# 39. Dashboard state dosyaları

`ScrapDashboardState.json` ve `ReworkDashboardState.json` core transactional migration’dan ayrı ele alınır.

Önce şu sorular cevaplanır:

- JSON raw imported spreadsheet cache mi?
- kullanıcı yorumu/ayar mı?
- gerçek historical source data mı?

Bu profiling tamamlanmadan generic analytics tablosuna otomatik import edilmez.

Dashboard HTML/JS asset’leri veri değildir; yeni web dashboard implementasyonuna referans tasarım/hesap mantığı olarak kullanılabilir.

---

# 40. Migration sıra planı

## Faz M0 — Freeze öncesi dry run

1. Legacy root read-only snapshot al.
2. Dosya manifesti: path, byte size, sha256.
3. CSV row counts çıkar.
4. Duplicate/business-key profiler çalıştır.
5. Reject/warning raporu üret.
6. İş sahibi conflict kararlarını verir.

## Faz M1 — Identity/support

1. users metadata
2. roles/permissions bootstrap
3. departments/catalogs/reason values
4. notification recipients
5. test catalog/groups
6. metrology devices

## Faz M2 — Engineering

1. product/mold clusters
2. drawings
3. drawing revisions
4. binary drawings
5. control points/versions
6. group areas

## Faz M3 — Operational masters/workflows

1. commissioning parent/children
2. mold bindings
3. production tickets
4. mold tickets
5. connection plan
6. mechanism quality
7. shift tracking/photos
8. error reports/actions/reviews/evaluations
9. INO
10. test requests/steps/attachments
11. package meter

## Faz M4 — Measurement history

1. inspection sessions by RecordId
2. eyes
3. measurements
4. visual controls
5. closed-eye sessions
6. measurement corrections
7. SPC corrections
8. quality→production tickets

Measurement history sonlara bırakılır çünkü birçok FK master/workflow’a referans verir.

## Faz M5 — Audit/notification events

Historical audit/email events import edilir.

## Faz M6 — Reconciliation

Sayımlar, FK, hash, state ve örnek record karşılaştırmaları.

---

# 41. Reconciliation zorunlulukları

Migration başarı kriteri yalnız “script hata vermedi” değildir.

## 41.1 Count reconciliation

Her authoritative source için:

```text
source rows
= imported rows
+ intentionally skipped rows
+ rejected rows
```

Fark sıfır olmalıdır.

## 41.2 Relationship reconciliation

- her Measurement RecordId target session’a map edilmiş mi?
- her VisualControl RecordId bir eye bulmuş mu?
- her Correction target measurement bulmuş mu?
- her QualityToProduction RecordId target inspection bulmuş mu?
- her TestStep target Request bulmuş mu?
- every commissioning child parent bulmuş mu?
- photo/attachment file index gerçek dosyayı bulmuş mu?

## 41.3 State reconciliation

- Completed commissioning → dört onay gerçekten ONAYLANDI mı?
- Completed package control validation snapshot uygun mu?
- Closed ticket close actor/time mevcut mu?
- Error report status evaluations ile tutarlı mı?
- Test request completed iken unresolved step var mı?

Mismatch silinmez; report edilir.

## 41.4 Measurement calculation reconciliation

Sample değil, mümkünse tüm parse edilebilir measurement satırlarında:

`recalculated_result == legacy Result`

oranı hesaplanır. Farklı satırlar exported CSV raporuna yazılır.

## 41.5 File reconciliation

Her migrated file için:

- source exists
- decrypt successful if needed
- target exists
- target SHA expected
- DB sha matches storage

---

# 42. Cutover checklist

1. Legacy desktop yazma işlemleri durdurulur.
2. Tüm kullanıcıların Local Draft kayıtlarını tamamlaması sağlanır.
3. Active STARTED binding listesi iş sahibiyle doğrulanır.
4. PendingTransactions boş/recovered olmalıdır.
5. Son `Data/` + `Drawings/` snapshot alınır.
6. Snapshot immutable/read-only hale getirilir.
7. Final migration çalıştırılır.
8. Reconciliation report %100 açıklanır.
9. Business smoke tests yapılır.
10. Web uygulaması write mode’a alınır.
11. Desktop app salt okunur veya kapalı hale getirilir.
12. Rollback window boyunca final legacy snapshot korunur.

---

# 43. Codex migration implementation contract

Codex şu kurallarla migration kodu yazmalıdır:

- `management command` veya ayrı `migration_tools` package.
- Aynı source snapshot üzerinde tekrar çalıştırılabilir/idempotent.
- `--dry-run`, `--source-root`, `--run-id`, `--only`, `--resume` seçenekleri.
- Her source için structured stats.
- Reject satırlarını log’a gömmek yerine DB/CSV report.
- Source dosyalara **asla write etme**.
- Migration sırasında application signals ile yanlış notification/email gönderme.
- `bulk_create` performans için kullanılabilir fakat business normalization/test atlanamaz.
- Büyük MeasurementRecords streaming/chunk ile okunmalı.
- Her batch transaction; tek dev transaction zorunlu değil.
- Binary migration yeniden çalıştığında hash/idempotency ile duplicate üretmemeli.
- Şifre/key değerleri loglanmamalı.

---

# 44. Migration testleri

Minimum otomatik test seti:

1. Turkish decimal parse
2. legacy date parse
3. YES/NO bool parse
4. scope normalization
5. lower tolerance sign correction
6. duplicate legacy key conflict
7. product grouping conflict
8. missing drawing file
9. encrypted drawing decrypt happy/fail path
10. RecordId → one session mapping
11. visual orphan reject
12. correction chain mapping
13. multiple STARTED binding import exception
14. completed test with pending step reconciliation
15. commissioning ŞARTLI ONAY status check
16. error report evaluator mismatch
17. package duplicate serial detection
18. attachment >50MB legacy anomaly
19. path traversal rejection
20. same import run rerun produces zero duplicate domain row

---

# 45. Migration çıktıları

Her dry/final run şu dosyaları üretmelidir:

```text
migration_report_<run>.json
migration_summary_<run>.md
rejects_<run>.csv
warnings_<run>.csv
key_conflicts_<run>.csv
missing_files_<run>.csv
measurement_result_mismatches_<run>.csv
state_mismatches_<run>.csv
```

Final cutover ancak blocker severity reject sayısı sıfır veya her satır için imzalı/recorded istisna kararı varsa yapılır.

---

## Sonuç

Legacy uygulamanın dosya yapısı bir veritabanı gibi davranmaktadır ancak her CSV aynı önemde değildir. Yeni sisteme geçişte esas amaç **dosyaları PostgreSQL’e kopyalamak değil, eski iş olaylarının anlamını kaybetmeden yeni domain modeline dönüştürmektir**. Özellikle measurement `RecordId`, control-point version/SPC key, ticket linkleri, commissioning child kayıtları ve error-report değerlendirme geçmişi migration’ın kritik doğrulama noktalarıdır.
