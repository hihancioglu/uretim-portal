# A Blok Kalite Kontrol — Web Dönüşüm Master Planı v1

**Tarih:** 26.08.2026  
**Kaynak:** `A_Blok_Kalite_Kontrol.zip` içindeki VB.NET WinForms kaynak kodu, README/notlar, yetki matrisi ve SQL hazırlık dosyaları  
**Hedef:** Mevcut masaüstü uygulamasını iş kuralları korunarak web tabanlı, merkezi, izlenebilir ve ölçeklenebilir bir sisteme dönüştürmek.

---

## 1. Yönetici Özeti

Mevcut uygulama basit bir “teknik resim ölçüm programı” olmaktan çıkmış; kalite, üretim, kalıphane, laboratuvar ve teknik resim süreçlerini kapsayan geniş bir operasyon portalına dönüşmüştür.

Kaynak pakette:

- 64 WinForms form sınıfı,
- 50 civarında CSV veri kaynağı,
- 24 tablo içeren ancak eksik kalan SQL Server geçiş şeması,
- rol/yetki matrisi,
- PDF/DWG/DXF işleme,
- ölçüm ve SPC,
- MSA/ölçüm cihazları,
- ticket süreçleri,
- kalıp bağlama,
- yeni kalıp devreye alma,
- vardiya/hata raporu,
- test/laboratuvar,
- paket sayaç kontrolleri,
- dashboardlar,
- audit ve oturum yönetimi

bulunmaktadır.

### Ana karar

Yeni sistem mevcut WinForms ekranlarının birebir HTML kopyası olmayacaktır. Mevcut kaynak kod **iş analizi ve kabul kriteri kaynağı** olarak kullanılacak; veri modeli ve uygulama mimarisi web için yeniden tasarlanacaktır.

### Önerilen teknoloji

- **Backend:** Python + Django
- **API:** Django REST Framework (gereken modüllerde)
- **Frontend:** Django Templates + HTMX + Alpine.js
- **UI:** Bootstrap tabanlı responsive arayüz
- **Database:** PostgreSQL
- **PDF görüntüleme:** PDF.js
- **Grafik:** Chart.js
- **Background jobs:** Celery + Redis
- **Dosya saklama:** ilk aşamada Linux filesystem; büyüme halinde MinIO/S3 uyumlu storage
- **Kimlik doğrulama:** Authentik/OIDC veya AD/LDAP
- **Reverse proxy/TLS:** Nginx
- **Deployment:** Docker Compose
- **Test:** pytest + pytest-django + Playwright

React/Vue ilk sürüm için önerilmez. Uygulamanın büyük bölümü form, liste, workflow, rapor ve yetki kontrolüdür; Django + HTMX daha düşük karmaşıklıkla yeterlidir.

---

# 2. Dönüşümün Temel İlkeleri

## 2.1. İş kuralları korunacak, teknik borç taşınmayacak

Korunacak:

- Rol bazlı kapsamlar
- TR/revizyon mantığı
- Ölçüm OK/NOK hesabı
- Kritik ölçüler ve SPC anahtarları
- Ölçüm versiyonu/revizyon geçmişi
- Ticket state değişimleri
- Kalıp bağlama akışı
- Test adım sırası
- Tamamlanmış kayıtların tarihsel bütünlüğü
- Audit kayıtları
- Teknik resim erişim kısıtları

Taşınmayacak:

- CSV dosya kilitleme mekanizması
- masaüstü DPI/resize workaround'ları
- WebView2 bağımlılığı
- WinForms updater/launcher
- lokal parola sistemi
- Outlook draft bağımlılığı
- SQL Server geçiş şemasındaki `NVARCHAR(MAX)` yaklaşımı
- dosya yolu gösteren lokal çalışma mantığı

## 2.2. PostgreSQL gerçek veri tipleri kullanılacak

Örnek:

- `boolean` — aktif/pasif, kritik/zorunlu
- `numeric(14,5)` — nominal, tolerans, ölçüm
- `integer` — sıra, göz sayısı, numune adedi
- `timestamptz` — olay zamanları
- `date` — termin/kalibrasyon tarihleri
- `uuid` — iç sistem anahtarları
- `jsonb` — audit before/after ve esnek metadata
- `text` — açıklamalar

## 2.3. Veritabanı constraint'leri iş kuralını destekleyecek

Örneğin:

- aynı teknik resim/revizyonda aynı ölçü kodu tekrarlanamaz,
- aktif revizyon tek olmalı,
- tamamlanmış paket sayaç kaydı normal kullanıcı tarafından değiştirilemez,
- bir kalıp için aynı anda izin verilmeyen ikinci STARTED binding oluşamaz,
- test adımı sıra kuralları servis katmanından geçmeden değiştirilemez.

## 2.4. Kritik işlemler transaction olacak

Özellikle:

- kalıp bağlama tamamlama + production ticket oluşturma,
- ölçüm kaydı + NOK ticket üretimi,
- vardiya kaydı + kalıp ticket oluşturma,
- test talebi + test step snapshot,
- teknik resim revizyon aktivasyonu,
- ölçüm düzeltme + audit,
- yeni kalıp devreye alma alt kayıtları.

`transaction.atomic()` ve gerektiğinde `select_for_update()` kullanılacaktır.

---

# 3. Mevcut Sistem Envanteri ve Hedef Modüller

## 3.1. Giriş Kalite Kontrol

Mevcut:

- Giriş kalite teknik resimleri
- Ölçüm girişi
- Ölçüm geçmişi
- Test talebi oluşturma

Hedef Django app:

`inspections` + `drawings` + `laboratory`

---

## 3.2. Plastikhane Kalite

Mevcut:

- Ölçüm girişi/geçmişi
- Kalite ticketları
- Plastikhane vardiya takip
- Hata raporları
- Kalıp tadilat bağlantısı
- SPC
- Hurda dashboard
- REWORK dashboard

Hedef:

`inspections`, `quality_tickets`, `shift_tracking`, `nonconformity`, `spc`, `analytics`

---

## 3.3. Mekanizma Kalite

Mevcut:

- Mekanizma teslim/kontrol
- uygun/uygun değil sonucu
- teslim ve kontrol zamanı
- INO-1 / INO-2 takip
- mekanizma vardiya takip
- e-posta raporları

Hedef:

`mechanism`, `ino`, `shift_tracking`

---

## 3.4. Üretim / Kalıp Yönetimi

Mevcut:

- Kalıp bağlama başlat/tamamla
- devam eden binding takibi
- planlanan kalıplar
- Excel import
- teknik resim arama
- kalıp binding dashboard
- üretim ticketları
- kalıp ticketları

Hedef:

`molding`, `production`, `tickets`

---

## 3.5. Kalıphane — Yeni Kalıp Devreye Alma

Kaynak kodda SQL hazırlık dosyasından daha ileri bir modül bulunmaktadır:

- devreye alma ana kaydı
- aşama/current stage
- ön kabul checklist
- deneme kayıtları
- aksiyonlar
- mekanik/ürün/proses onayları
- nihai karar
- koşullu onay
- devreye alma ile bağlı ölçüm kayıtları

Hedef:

`commissioning`

Bu modül PostgreSQL şemasında **birinci sınıf domain** olarak ele alınmalıdır; CSV'deki 4 ayrı dosya doğrudan tek büyük tabloya gömülmemelidir.

---

## 3.6. Teknik Resim / Engineering

Mevcut:

- Ürün/TR tanımları
- Plastik Resmi / Giriş Kalite Kontrol Resmi / TR Resmi kapsamları
- revizyon
- şifreli PDF
- kontrol noktaları
- X/Y yüzdesi
- ölçüm grubu alanları
- PDF render/zoom
- CAD dimension import
- DXF import
- AutoCAD Core Console ile DWG dimension import

Hedef:

`products` + `drawings` + `control_points` + opsiyonel `cad_worker`

---

## 3.7. SPC / MSA

Mevcut:

- SPC dashboard
- riskli ölçüler
- kontrol noktası versiyonları
- geçmiş limit düzeltmesi
- ölçüm cihazları
- kalibrasyon bilgileri

Hedef:

`spc` + `metrology`

---

## 3.8. Laboratuvar / Test

Mevcut:

- test kataloğu
- test grupları
- talep oluşturma
- çoklu talep nedeni
- test atama
- test step sırası
- step complete/skip/reopen
- işlem alma
- sonuçlandırma
- iptal
- ekler
- e-posta eventleri/alıcıları
- paket sayaç kontrolü

Hedef:

`laboratory` + `package_meter`

---

## 3.9. Yönetim / Sistem

Mevcut:

- kullanıcılar
- aktif sessionlar
- session sonlandırma isteği
- running instances
- audit
- critical data journal
- data health
- permission matrix
- updater

Hedef:

`accounts`, `audit`, `system_health`

Updater/running desktop instance mantığı web sürümünde kaldırılır.

---

# 4. Hedef Repository Yapısı

```text
quality_portal/
├── manage.py
├── pyproject.toml
├── compose.yml
├── .env.example
├── config/
│   ├── settings/
│   │   ├── base.py
│   │   ├── dev.py
│   │   └── prod.py
│   ├── urls.py
│   ├── asgi.py
│   └── celery.py
│
├── apps/
│   ├── accounts/
│   ├── audit/
│   ├── core/
│   ├── products/
│   ├── drawings/
│   ├── inspections/
│   ├── spc/
│   ├── metrology/
│   ├── tickets/
│   ├── molding/
│   ├── commissioning/
│   ├── shift_tracking/
│   ├── nonconformity/
│   ├── mechanism/
│   ├── ino/
│   ├── laboratory/
│   ├── package_meter/
│   ├── analytics/
│   └── notifications/
│
├── templates/
├── static/
├── tests/
├── scripts/
│   └── legacy_import/
└── docs/
    ├── WEB_DONUSUM_MASTER_PLANI_V1.md
    ├── DOMAIN_RULES.md
    ├── PERMISSION_MATRIX.md
    ├── LEGACY_MAPPING.md
    └── ADR/
```

## Kod yerleşim standardı

Her domain app mümkün olduğunca şu yapıda tutulmalı:

```text
models.py
selectors.py       # read/query işlemleri
services.py        # state changing business logic
permissions.py
forms.py
views.py
urls.py
admin.py
tasks.py           # varsa background işler
api/                # gerekiyorsa DRF
migrations/
tests/
```

**İş kuralı view içinde veya model `save()` içinde dağınık tutulmamalıdır.**

---

# 5. PostgreSQL Domain Modeli v1

Aşağıdaki model v1 hedefidir. Kodlama öncesinde ER diyagramına dönüştürülecektir.

## 5.1. Accounts / Security

### `accounts_userprofile`

Django User ile 1:1:

- `user_id`
- `external_username`
- `display_name`
- `department_id`
- `is_active`
- `last_synced_at`

Parola hash/salt yeni PostgreSQL sistemine taşınmayacak; AD/OIDC kullanılır.

### Django Group/Permission

Mevcut roller başlangıç grupları olarak seed edilir:

- Üretim Kullanıcısı
- Üretim Etiket
- Üretim Yöneticisi
- Plastikhane Kalite Kontrol
- Kalite Kontrol Yöneticisi
- Giriş Kalite Kontrol
- Mekanizma Kalite Kontrol
- Mekanizma Yöneticisi
- Kalite Laboratuvar
- Teknik Resim
- Planlama
- Yönetici
- Admin

Legacy `Kalite Kontrol Kullanıcısı` → `Plastikhane Kalite Kontrol` map edilir.

---

## 5.2. Referans / Core

Önerilen tablolar:

- `core_department`
- `core_machine`
- `core_material`
- `core_color`
- `core_reason_code`
- gerektiğinde lookup tabloları

Her dropdown'ı tabloya çevirmek zorunlu değildir. Gerçek master data olan değerler tablo, sabit küçük enum'lar Django Choices olabilir.

---

## 5.3. Products / Molds

### `products_product`

- `id UUID PK`
- `name`
- `plastic_code`
- `material`
- `color_name`
- `is_active`
- `created_by`
- `created_at`
- `updated_at`

### `products_mold`

- `id`
- `mold_code`
- `cavity_count`
- `is_active`

### `products_product_mold`

Gerekirse bir ürünün birden fazla kalıpla ilişkisini destekler.

> Mevcut kaynakta önemli not: Kalıp ticketı için doğru anahtar yalnız TR değildir; `TR + Kalıp Kodu` ilişkisi korunmalıdır.

---

## 5.4. Drawings

### `drawings_drawing`

- `id`
- `product_id`
- `tr_code`
- `scope`
  - PLASTIC
  - INCOMING_QUALITY
  - TR
- `is_active`

Önerilen unique:

`(tr_code, scope, product_id)` veya canlı veriye göre sadeleştirilmiş eşdeğeri.

### `drawings_revision`

- `id`
- `drawing_id`
- `revision_code`
- `status` (`DRAFT`, `ACTIVE`, `SUPERSEDED`, `WITHDRAWN`)
- `effective_from`
- `effective_to`
- `file_id`
- `created_by`
- `created_at`
- `approved_by`
- `approved_at`
- `change_reason`

Unique:

`(drawing_id, revision_code)`

Aynı drawing için DB seviyesinde tek aktif revizyon kuralı partial unique index ile korunabilir.

### `core_file_object`

- `id`
- `storage_key`
- `original_filename`
- `mime_type`
- `size_bytes`
- `sha256`
- `created_by`
- `created_at`
- `encryption_key_version` (uygulanırsa)

Gerçek dosya DB BLOB alanında tutulmaz.

---

# 6. Kontrol Noktası Modeli

Mevcut `ControlPoints.csv` içinde hem kimlik hem de versiyon bilgisi aynı satırdadır. Web sürümünde ikiye ayırmak daha güvenlidir.

### `control_points_controlpoint`

Stabil mantıksal ölçü kimliği:

- `id`
- `drawing_id`
- `spc_key`
- `logical_code`
- `created_at`

### `control_points_controlpointversion`

- `id`
- `control_point_id`
- `drawing_revision_id`
- `measure_version`
- `measure_code`
- `measure_name`
- `nominal numeric`
- `lower_tolerance numeric`
- `upper_tolerance numeric`
- `lower_limit numeric`
- `upper_limit numeric`
- `unit`
- `page_no`
- `x_percent`
- `y_percent`
- `is_mandatory`
- `measurement_group`
- `sample_frequency`
- `is_critical`
- `sort_no`
- `valid_from`
- `valid_to`
- `change_reason`
- `is_active`

### Kural

Control point değişikliğinde eski versiyon overwrite edilmez. Yeni version yaratılır.

Bu, mevcut `SpcKey`, `MeasureVersion`, `ValidFrom`, `ValidTo`, `ChangeReason` mantığını doğal biçimde karşılar.

### `control_points_grouparea`

- drawing_revision
- group_name
- page_no
- left/top/right/bottom percent

---

# 7. Ölçüm / Inspection Modeli

Mevcut CSV'deki her ölçümü düz satır olarak saklamak yerine oturum + göz + ölçü hiyerarşisi kurulacaktır.

## `inspections_inspectionsession`

- `id`
- `drawing_revision_id`
- `scope`
- `lot_no`
- `serial_no`
- `eye_count`
- `operator_user_id`
- `client_name/ip` (gerekirse)
- `production_ticket_id`
- `commissioning_id`
- `started_at`
- `completed_at`
- `overall_result`
- `status`

## `inspections_inspectioneye`

- `id`
- `session_id`
- `eye_no`
- `is_closed`
- `closed_reason`
- `closed_by`
- `closed_at`

Unique `(session_id, eye_no)`.

## `inspections_measurement`

- `id`
- `inspection_eye_id`
- `control_point_version_id`
- `measured_value`
- `result`
- `note`
- `measured_by`
- `measured_at`

### Snapshot alanları

Tarihsel bütünlük için ölçüm satırına aşağıdaki snapshotlar da kaydedilir:

- `measure_code_snapshot`
- `measure_name_snapshot`
- `nominal_snapshot`
- `lower_limit_snapshot`
- `upper_limit_snapshot`
- `unit_snapshot`
- `spc_key_snapshot`
- `measure_version_snapshot`

Kontrol noktası ileride değişse bile eski ölçüm değişmez.

## `inspections_measurementrevision`

Düzeltme işlemi için:

- measurement_id
- revision_no
- old_value/new_value
- old_result/new_result
- reason
- changed_by
- changed_at

Düzeltme nedeni zorunlu.

## `inspections_visualcontrol`

Mevcut VisualControlRecords ayrı domain entity olarak korunur.

---

# 8. Ölçüm OK/NOK Motoru

Tek bir ortak domain fonksiyonu kullanılmalıdır.

```text
lower_limit <= measured_value <= upper_limit => OK
aksi => NOK
```

Özel legacy kuralı:

Mevcut uygulamada kullanıcı alt toleransı pozitif `1` girse bile `-1` olarak yorumlanıyor. Yeni UI'da bu belirsizlik kaldırılmalı:

- ekranda alt toleransın işaretli hali net gösterilmeli,
- legacy import sırasında pozitif LowerTol normalize edilmeli,
- yeni kayıt API'si doğrulanmış numeric değer almalı.

Bu kural için regression test zorunludur.

---

# 9. SPC Modeli

## `spc_limit_correction`

Mevcut `SpcLimitCorrections.csv` kaybı olmadan import edilir.

Alanlar:

- spc_key
- date_from/date_to
- old_limits jsonb
- new_nominal/lower/upper
- affected_rows
- result_changed_rows
- reason
- changed_by/at

### SPC hesaplama servisi

Başlangıçta:

- count
- min/max
- mean
- stddev
- Cp
- Cpk
- trend
- NOK oranı

Sonrasında kontrol kartları genişletilebilir.

Hesaplar view template içinde yapılmaz; selector/analytics service katmanında olur.

---

# 10. MSA / Ölçüm Cihazları

Mevcut `MeasurementDevices.csv`, SQL Server şemasında yoktur. PostgreSQL tasarımında eksiksiz taşınacaktır.

### `metrology_device`

- device_id/business code
- fixed_asset_no
- name
- serial_no
- brand/model
- device_type
- measurement_range
- resolution
- unit
- location
- reference_device
- usage_status
- status
- calibration_period_months
- calibration_date
- calibration_due_date
- organization
- responsible
- ilgili ISO bayrakları
- audit alanları

Kalibrasyon yaklaşınca dashboard uyarısı eklenebilir.

---

# 11. Ticket ve Workflow Modeli

## 11.1. Kalıp Binding

### `molding_binding`

Mevcut `MoldBindingRecords.csv` karşılığı.

Durumlar:

```text
STARTED -> COMPLETED
```

Kritik kurallar:

1. Aynı kalıp için aynı anda ikinci aktif bağlama engellenmeli.
2. Kullanıcı yalnız yetkili kayıtları görebilmeli.
3. Makine değişiminde change reason zorunlu olmalı.
4. Completion transaction'ı production ticket yaratımıyla birlikte commit olmalı.
5. Açık mold ticket varsa UI kırmızı uyarı vermeli.

## 11.2. Production Ticket

`production_ticket`

- binding ile ilişki
- seen_by_quality/seen_at
- closed_by/closed_at
- close_note

## 11.3. Mold Ticket

`mold_ticket`

- mold_id
- drawing revision/product snapshot
- severity
- problem_type
- problem_description
- action_plan
- source plastic shift record
- close metadata

## 11.4. Quality → Production Ticket

`quality_production_ticket`

- inspection/record source
- source type
- NOK measurement/visual counts
- production seen state
- close state

State değişimleri ayrı service fonksiyonlarından yapılmalı.

---

# 12. Kalıp Bağlama Planı

### `molding_connection_plan_import`

Excel import header bilgisi:

- source file hash/name
- sheet
- imported_by/at

### `molding_connection_plan_row`

- machine
- current mold
- first/second planned mold
- rack no
- plastic code
- TR
- sort/order

Import tek transaction olmalıdır.

Aynı dosyanın yanlışlıkla tekrar yüklenmesini hash ile önleyebiliriz.

---

# 13. Yeni Kalıp Devreye Alma

Ayrı domain tutulacaktır.

## Tablolar

- `commissioning_commissioning`
- `commissioning_checklistitem`
- `commissioning_trial`
- `commissioning_action`
- `commissioning_approval`

### Approval tablosu tercih nedeni

Mevcut ana satırdaki:

- MechanicalApproval
- ProductApproval
- ProcessApproval

alanlarını ayrı approval tablosu yapmak ileride yeni onay adımı eklemeyi kolaylaştırır.

### Önerilen state machine

```text
DRAFT
 -> PRE_ACCEPTANCE
 -> TRIAL
 -> QUALITY_VALIDATION
 -> APPROVAL
 -> APPROVED
 -> CONDITIONALLY_APPROVED
 -> REJECTED/CLOSED
```

Gerçek state isimleri canlı kullanım doğrulamasından sonra kesinleştirilir.

Devreye alma ölçümleri `inspection_session.commissioning_id` ile bağlanır.

---

# 14. Plastikhane / Mekanizma Vardiya

Ortak base mantık uygulanabilir ancak iki sürecin UI ve izinleri ayrıdır.

## `shift_tracking_shiftrecord`

- area/type: PLASTIC / MECHANISM
- occurred_at
- defective_quantity text/numeric ihtiyaca göre
- responsible
- product/name code
- problem
- action_taken
- yellow_card
- mold_modification
- error_report
- test_performed
- creator/updater

## `shift_tracking_photo`

Mevcut `ShiftTrackingPhotos.csv` + gerçek dosyalar yeni file storage modeline taşınır.

---

# 15. Hata / Uygunsuzluk Raporu

Mevcut PlasticShiftErrorReport çok kapsamlıdır. Tek dev tabloyu birebir taşımak ilk migration için mümkün olsa da hedef modelde bölmek daha sağlıklıdır.

Öneri:

- `nonconformity_report`
- `nonconformity_action`
- `nonconformity_review`
- `nonconformity_evaluation`
- `nonconformity_evaluator_assignment`
- `notification_event`

Action1..Action5 kolonları yerine `nonconformity_action` satırları kullanılmalıdır.

Bu, mevcut veride 5 aksiyon sınırını kaldırır.

---

# 16. Mekanizma Kalite Kontrol

### `mechanism_quality_delivery`

- delivery/control status
- delivered_at/by
- incoming eye count
- product code/name
- mounted mechanism counter
- delivery explanation
- control explanation
- result suitable/unsuitable
- controlled_by/at

Kurallar:

- Mekanizma kalite yeni teslim oluşturamazsa bu permission ile korunmalı.
- Bekleyen teslim sonucu yalnız yetkili rollerce verilmeli.
- Yönetici / Mekanizma Yöneticisi salt okunur kapsamı korunmalı.

---

# 17. İNO Modülü

INO kaynakları ayrı modül olarak tutulur.

İlk aşamada legacy kolon yapısı analiz edilerek PostgreSQL'e normalize edilir.

Önemli gereksinimler:

- kolon bazlı filtreleme
- rol bazlı edit alanları
- onay rolü
- Yönetici salt okunur
- INO-1 / INO-2 ayrımı

Bu modül bağımsız migration work package olmalıdır.

---

# 18. Laboratuvar / Test Request State Machine

Mevcut akış korunacaktır.

Ana durumlar:

```text
NEW -> PROCESSING -> COMPLETED
          |
          -> CANCELLED
```

Legacy Türkçe durumlar import sırasında enum'a map edilir.

## `laboratory_testrequest`

- requesting_department
- requested_department
- reason(s)
- product/TR
- sample_quantity
- priority
- due_date
- reference report no
- requester explanation
- accepted metadata
- completion metadata
- lab report no/result/explanation
- cancellation metadata

Çoklu request reason string yerine relation tablosu veya JSONB değil; tekrar raporlanacaksa relation önerilir.

## `laboratory_testrequeststep`

- request
- sort_no
- test snapshot
- status
- result
- explanation
- completed metadata
- skipped metadata
- reopened metadata

### Zorunlu kurallar

1. Talep kabul edilmeden step sonucu girilemez.
2. Test adımları sıralı işlenir.
3. Yetkili override olmadan sıradaki test atlanamaz.
4. Skip için gerekçe zorunlu.
5. Reopen geriye doğru ve sıra bütünlüğünü koruyarak yapılır.
6. Tüm step'ler resolved olmadan genel laboratuvar sonucu tamamlanamaz.
7. Request owner kendi açık talebini iptal edebilir; diğer iptal hakları role göre belirlenir.
8. Her step actor + timestamp saklar.

Bu akış için unit test sayısı yüksek tutulmalıdır.

---

# 19. Test Kataloğu ve Grupları

- `laboratory_testcatalog`
- `laboratory_testgroup`
- `laboratory_testgroupitem`

Mevcut `TestsText` gibi delimiter string yerine many-to-many relation kullanılmalıdır.

Request oluşturulurken seçilen testler **snapshotlanmalıdır**; katalog daha sonra değişse bile eski talep etkilenmemelidir.

---

# 20. Paket Sayaç Kontrolleri

## `package_meter_control`

Header bilgiler ve status.

## `package_meter_line`

Her sayaç/seri numarası için satır.

Kurallar:

- draft düzenlenebilir,
- completed normal kullanıcı için immutable/read-only,
- delete sadece Admin,
- overall result server-side hesaplanmalı,
- izin verilen range listesi domain validation olarak tanımlanmalı,
- completed işlem transaction olmalı.

Mevcut allowed ranges:

`40, 50, 63, 80, 100, 125, 160, 200, 250, 315, 400, 500, 630, 800, 1000`

---

# 21. Teknik Resim Web Viewer

## 21.1. PDF

Web sürümünde masaüstündeki PNG render workaround'ları kaldırılır.

Hedef:

- PDF.js ile browser render
- canvas üzerinde overlay layer
- control point balonları HTML/SVG overlay
- koordinatlar yüzde veya normalized PDF coordinates
- zoom/pan sırasında overlay aynı transform'u takip eder
- çok sayfalı PDF desteklenir

### Önerilen coordinate format

DB'de:

- `page_no`
- `x_ratio numeric(8,6)` — 0..1
- `y_ratio numeric(8,6)` — 0..1

Legacy XPercent/YPercent importta `/100` yapılır.

## 21.2. Kontrol noktası editörü

Teknik Resim rolü/Admin:

1. revision açar,
2. PDF üzerinde noktaya tıklar,
3. ölçü adı/nominal/tolerans/grup/kritik bilgisi girer,
4. server limitleri hesaplar,
5. preview görür,
6. kaydeder.

## 21.3. Dosya güvenliği

Kullanıcıya fiziksel storage path verilmez.

Download/view endpoint:

- permission check
- aktif/passive revision check
- audit gerektiğinde
- streaming response / Nginx X-Accel-Redirect

---

# 22. DWG / DXF Stratejisi

Bu, projenin ayrı teknik iş paketi olmalıdır.

## DXF

Linux/Python tarafında `ezdxf` ile işlenebilir.

## DWG

Mevcut uygulama `accoreconsole.exe` / AutoCAD bağımlılığı kullanır.

Önerilen v1:

### Windows CAD Worker

```text
Django Web
   |
   | job
   v
Redis/Celery veya HTTP queue
   |
   v
Windows CAD Worker
AutoCAD Core Console
   |
   v
JSON/DXF preview/result
```

Web sunucusunun Windows/AutoCAD bağımlılığı olmaz.

Alternatif olarak kullanım hacmi düşükse ilk release'te sadece PDF + DXF desteklenip DWG worker ikinci faza alınabilir.

---

# 23. Yetkilendirme Tasarımı

Mevcut rol matrisi yeni sistemde başlangıç seed verisi olacaktır.

Permission isimleri ekran değil **işlem** bazlı olmalıdır.

Örnek:

```text
drawings.view
drawings.manage
drawings.delete
control_points.view
control_points.manage
inspections.create
inspections.view
inspections.correct
inspections.delete
spc.view
spc.correct_historical_limits
molding.binding.start
molding.binding.complete
molding.plan.import
tickets.mold.create
tickets.mold.close
laboratory.request.create
laboratory.request.process
laboratory.steps.override
package_meter.complete
admin.audit.view
```

## Scope permission

Sadece permission yetmez; record-level scope gerekir.

Örneğin:

- GKK → yalnız `INCOMING_QUALITY`
- Plastikhane Kalite → yalnız `PLASTIC`
- Yönetici → geniş read-only
- Üretim Kullanıcısı → kendi aktif bindingleri

Bu kontroller ortak `permissions.py` / selector seviyesinde uygulanmalıdır.

Template'de buton gizlemek güvenlik değildir; server endpoint ayrıca doğrulamalıdır.

---

# 24. Authentication

## Tercih 1 — Authentik + AD + OIDC

Önerilen.

Avantaj:

- merkezi SSO
- AD parola politikası
- uygulama parola saklamaz
- ileride MFA eklenebilir
- group claim ile rol mapping

## Tercih 2 — Doğrudan LDAP

Daha basit fakat SSO/OIDC kadar esnek değil.

### Legacy kullanıcı geçişi

`Users.csv` parola hashleri yeni sisteme taşınmaz.

Taşınabilecek:

- username
- eski role mapping
- active flag

Kimlik doğrulama AD'den yapılır.

---

# 25. Audit / İzlenebilirlik

## `audit_event`

- id UUID
- timestamp
- actor_user
- actor_role snapshot
- event_type
- entity_type
- entity_id
- drawing/product references
- before_data jsonb
- after_data jsonb
- reason
- source_ip
- user_agent
- request_id/correlation_id

Audit tablosu uygulama iş tablolarıyla aynı transaction içinde yazılmalıdır.

Özellikle zorunlu audit eventleri:

- drawing revision created/activated
- control point changed/revised/deactivated
- measurement correction
- SPC historical correction
- ticket opened/seen/closed
- test step skip/reopen
- test request completion/cancel
- package meter completion/delete
- commissioning approval/final decision
- role/permission change

---

# 26. Bildirim / E-posta

Outlook draft entegrasyonu kaldırılır.

Hedef:

`notification_rule`

- event_type
- department/scope
- recipient type TO/CC
- recipient address/group
- active

`notification_event`

- business event id
- recipients snapshot
- send status
- attempts
- sent_at
- error

Celery işi:

```text
business transaction commit
    -> enqueue notification
    -> worker sends Graph/SMTP
```

Kritik business transaction e-posta hatası nedeniyle rollback olmamalıdır.

---

# 27. Dashboard / Analytics

Mevcut HTML dashboard tasarımları görsel referans olarak yeniden kullanılabilir.

Dashboard sorguları PostgreSQL üzerinden yapılır.

Ayrı app:

`analytics`

İlk KPI'lar:

- açık kalite ticket
- aktif binding
- bekleyen mekanizma kontrol
- açık test talebi
- eksik teknik resim
- günlük ölçüm / NOK
- hurda
- rework
- kalıp bağlama süreleri
- kalibrasyon yaklaşan cihazlar

Ağır sorgular için ileride materialized view düşünülebilir; ilk sürümde erken optimizasyon yapılmaz.

---

# 28. Legacy Veri Göçü

## 28.1. Kaynaklar

Kaynak kodda yaklaşık 50 CSV veri kaynağı referansı vardır.

Mevcut SQL Server `Schema.sql` sadece 24 tablo kapsar. Şu önemli veriler SQL şemasında eksiktir:

- MeasurementCorrections
- MeasurementDevices
- SpcLimitCorrections
- ShiftTrackingPhotos
- MechanismShiftTracking
- PlasticShiftErrorReport ailesi
- NewMoldCommissioning ailesi
- TestRequestAttachments
- notification/email eventleri
- running/session yardımcı kayıtları
- INO verileri

Bu nedenle SQL Server ara şemasına migration önerilmez.

## 28.2. Migration aşamaları

### A. Discovery

Canlı sistemden kontrollü export alınır:

- CSV'ler
- Drawings
- attachments/photos
- `.pdf.enc` dosyaları
- varsa encryption key/config

### B. Profiling

Her CSV için:

- row count
- duplicate key
- boş alan
- tarih formatları
- bozuk numeric
- orphan id
- status values
- file existence

raporu üretilir.

### C. Staging

PostgreSQL'de doğrudan production tablolarına import edilmez.

İlk import staging tablolarına veya Python mapping katmanına yapılır.

### D. Transform

- YES/NO → boolean
- Türkçe/İngilizce status → enum
- tarihler → timestamptz/date
- decimal comma/dot normalize
- LowerTol legacy pozitif değer düzeltmesi
- role normalization
- drawing scope normalization
- duplicate detection

### E. Load

FK sırası:

1. users/reference data
2. products/molds
3. drawings/revisions/files
4. control points/versions/groups
5. inspections
6. corrections/SPC
7. tickets/bindings/plans
8. shifts/nonconformity/mechanism
9. commissioning
10. laboratory/package meter
11. notifications/audit

### F. Reconciliation

Her dataset için:

- source row count
- imported count
- skipped count
- rejected count
- hash/file validation

çıktısı alınır.

Migration script **tekrar çalıştırılabilir/idempotent** olmalıdır.

---

# 29. Cutover Stratejisi

Big-bang yerine kontrollü geçiş.

## Önerilen yöntem

### Faz 1 — Read-only pilot

Web portal eski veriyi import ederek read-only doğrular.

### Faz 2 — Yeni teknik resim + ölçüm pilotu

Seçilen küçük kullanıcı grubu web'de işlem yapar.

### Faz 3 — Modül modül geçiş

- engineering
- inspection
- ticket/binding
- laboratory
- diğer operasyonlar

### Son kesim

Belirlenen tarihte masaüstü uygulama write işlemlerine kapatılır.

Son delta export/import alınır.

Web sistem primary olur.

Masaüstü kısa süre salt okunur arşiv olarak tutulabilir.

---

# 30. UI / UX Standartları

- Desktop 1920x1080 ana hedef
- 1366x768 destek
- tablet landscape destek
- ölçüm ekranında klavye ile hızlı giriş
- Enter → sonraki ölçü
- NOK → belirgin fakat erişilebilir durum
- current measure balonu/highlight
- role göre menü
- filtreler URL querystring ile korunabilir
- tablolarda pagination/sort/filter
- server-side büyük dataset sorguları
- destructive action confirmation
- read-only mod açık biçimde gösterilmeli
- toast yerine kritik sonuçlar kalıcı feedback olarak görünmeli

---

# 31. API Stratejisi

Tüm sistemi SPA/API yapmak gerekli değildir.

DRF şu durumlarda kullanılmalı:

- PDF/control point AJAX işlemleri
- ölçüm hızlı save
- chart data
- CAD worker integration
- dış sistem entegrasyonu
- ileride mobil istemci

Standart CRUD ekranları Django view + HTMX ile yapılabilir.

---

# 32. Güvenlik Baseline

- HTTPS only
- Secure/HttpOnly/SameSite cookies
- CSRF protection
- OIDC/LDAP SSO
- role + object scope authorization
- file access endpoint authorization
- upload MIME + extension + size validation
- anti-path-traversal
- random storage names
- SHA-256 file integrity
- CSP
- security headers
- rate limiting login/API gerekiyorsa
- audit
- secretlerin `.env` yerine production secret yönetiminde tutulması
- DB user least privilege
- PostgreSQL network yalnız app hostlarına açık
- backup encryption
- restore testi

PDF'lerin ayrıca application-level AES-GCM ile şifrelenmesi gerekiyorsa storage adapter katmanına eklenebilir; encryption key dosya yanında tutulmaz.

---

# 33. Deployment Topolojisi

## Başlangıç

```text
Ubuntu VM
│
├── nginx
├── quality-web (Django/Gunicorn)
├── quality-worker (Celery)
├── redis
└── postgres
```

Üretim büyüdüğünde PostgreSQL ayrı VM'e alınabilir.

### Önerilen başlangıç kaynak

- 4–8 vCPU
- 8–16 GB RAM
- OS/app disk 100 GB
- drawing/attachment data ayrı disk
- günlük DB backup
- file storage backup

İlk günden container volume backup/restore prosedürü yazılmalıdır.

---

# 34. Observability

- structured JSON application logs
- request correlation id
- `/health/live`
- `/health/ready`
- DB health
- Redis health
- Celery queue health
- failed notification count
- failed CAD job count
- 5xx count
- login/auth failure audit

Mevcut izleme altyapısına syslog/OpenObserve veya Prometheus endpoint ile bağlanabilir.

---

# 35. Test Stratejisi

## Unit tests

Özellikle:

- tolerance normalization
- OK/NOK
- control point revision
- permission scope
- binding state machine
- ticket transitions
- test request step ordering
- package meter calculations
- commissioning approval flow

## Integration tests

- PostgreSQL transaction rollback
- concurrent binding start
- concurrent test step update
- file upload/revision activation
- AD/OIDC role mapping

## E2E / Playwright

En kritik kullanıcı senaryoları:

1. Teknik resim yükle → kontrol noktası oluştur
2. Kalite kullanıcısı ölçüm yap → kayıt geçmişini gör
3. NOK → ticket akışı
4. Binding başlat → tamamla → ticket
5. Test request → accept → steps → complete
6. Paket sayaç draft → complete → immutable
7. Yönetici read-only erişim
8. Yetkisiz endpoint doğrudan çağrısı → 403

---

# 36. Codex Çalışma Kuralları

Codex'e repo verildiğinde aşağıdaki kurallar kök `AGENTS.md` içinde yer almalıdır.

1. Legacy VB.NET kaynakları referanstır; değiştirilmez.
2. Yeni web kodu ayrı klasörde oluşturulur.
3. Bir faz tamamlanmadan sonraki domain'e geçilmez.
4. Her business rule önce test ile temsil edilir.
5. View içinde business logic yazılmaz.
6. PostgreSQL dışı storage/CSV runtime backend olarak kullanılmaz.
7. Booleans string olarak tutulmaz.
8. Parasal/ölçüsel decimal değerlerde float kullanılmaz.
9. DateTime timezone-aware olmalıdır.
10. Destructive action audit üretir.
11. Permission sadece UI gizleme ile çözülmez; server-side uygulanır.
12. Legacy mapping dokümanı güncellenmeden migration kodu merge edilmez.
13. Yeni dependency ekleme gerekçesi yazılır.
14. Migration reversibility/veri koruma kontrol edilir.
15. Her PR/work package sonunda testler çalıştırılır.

---

# 37. Uygulama Fazları

## FAZ 0 — Analiz Kilidi ve Test Envanteri

### Amaç

Kod yazmadan önce legacy davranışı kayıt altına almak.

### İşler

- tüm form/domain eşlemesi
- tüm CSV header mapping
- tüm status değerleri
- permission matrisi
- business rule katalogu
- mevcut PDF/CAD akışları
- email eventleri
- canlı veri profiling script tasarımı

### Çıktılar

- `DOMAIN_RULES.md`
- `LEGACY_MAPPING.md`
- `PERMISSION_MATRIX.md`
- ADR-001 Architecture
- ADR-002 Authentication
- ADR-003 Drawing Storage
- ADR-004 CAD Strategy

### Exit criterion

Kritik legacy işlevlerin hangi yeni module/service'e taşınacağı belirsiz kalmamalı.

---

## FAZ 1 — Platform Skeleton

### İçerik

- Django proje
- PostgreSQL
- Redis
- Celery
- Nginx/dev proxy
- Docker Compose
- settings separation
- health checks
- base layout
- login/SSO stub
- audit middleware/request id
- pytest setup

### Exit criterion

`docker compose up` sonrası uygulama + DB + worker ayağa kalkar ve CI testleri geçer.

---

## FAZ 2 — Accounts + Yetki + Core Masters

### İçerik

- OIDC/LDAP
- role/group seed
- permission seed
- scope authorization helpers
- departments/machines temel master data
- admin audit

### Exit criterion

Tüm 13 rol ile permission regression testleri geçer.

---

## FAZ 3 — Products + Drawings + Revisions + File Storage

### İçerik

- product/mold
- drawing/scope
- revision
- upload
- SHA-256
- active revision
- PDF.js viewer
- read-only drawing search
- scope filtering

### Exit criterion

Teknik Resim rolü revision yükleyebilir; üretim rollerinin sadece yetkili aktif plastik resmini görmesi doğrulanır.

---

## FAZ 4 — Control Points

### İçerik

- control point stable identity
- versions
- tolerance calculation
- page/x/y
- group areas
- sort
- critical/mandatory/sample frequency
- revision UI

### Exit criterion

Legacy FIX23 ve control point version davranışları testlerle doğrulanır.

---

## FAZ 5 — Inspection / Measurement

### İçerik

- session/eye/value
- visual control
- closed eye
- draft autosave gerekiyorsa Redis/DB draft
- keyboard workflow
- correction
- history
- read-only manager
- legacy snapshot fields

### Exit criterion

Gerçek bir TR üzerinde çok gözlü ölçüm baştan sona web'de tamamlanabilir.

---

## FAZ 6 — SPC + Metrology

- SPC selectors
- dashboard
- riskli ölçüler
- historical correction
- measurement devices
- calibration reminders

---

## FAZ 7 — Ticket + Mold Binding + Connection Plan

- production ticket
- quality→production
- mold ticket
- binding state machine
- plan Excel import
- dashboard
- transaction tests

Bu faz concurrency testi gerektirir.

---

## FAZ 8 — Shift + Error Report + Mechanism + INO

İki alt sprint önerilir:

A. vardiya + mekanizma  
B. hata raporu + evaluator workflow + INO

---

## FAZ 9 — Laboratory + Package Meter

- catalog/groups
- request state machine
- step workflow
- attachments
- email events
- package meter

Test step state-machine için kapsamlı regression suite şarttır.

---

## FAZ 10 — New Mold Commissioning

- commissioning
- checklist
- trials
- actions
- approvals
- measurement link

Bu modül legacy SQL şemasında eksik olduğu için migration özel kontrol ister.

---

## FAZ 11 — Analytics / Dashboard / Notifications

- main dashboard
- hurda
- rework
- binding KPI
- email/Graph worker
- recipient admin

Mevcut HTML dashboardlardan tasarım ve hesap referansı alınır.

---

## FAZ 12 — Migration Rehearsal

- gerçek data copy üzerinde import
- reconciliation
- performance test
- permission test
- user acceptance
- issue remediation

En az iki tam migration rehearsal önerilir.

---

## FAZ 13 — Production Cutover

- write freeze
- final backup
- delta export
- final import
- validation counts
- file hash validation
- smoke tests
- web production enable
- desktop read-only/disable

---

# 38. Önerilen İlk MVP Sınırı

Bütün sistemi bir anda bitirmeye çalışmamak gerekir.

### MVP-1

1. Authentication + roles
2. Product
3. Drawing/revision
4. PDF viewer
5. Control point
6. Measurement entry
7. Measurement history/correction
8. Basic audit

Bu, uygulamanın çekirdek değerini web'e taşır ve mimarinin doğruluğunu kanıtlar.

### MVP-2

- SPC/MSA
- ticket
- binding

### MVP-3

- shift/mechanism/error report
- lab/package meter
- commissioning
- analytics/email

---

# 39. İlk Codex Görev Paketi

Codex'e tüm projeyi bir kerede yazdırmak yerine aşağıdaki ilk görev verilmelidir.

## Codex Prompt — WP-001

```text
You are working in the A Blok Kalite Kontrol migration repository.

Goal:
Create only the web platform skeleton and architecture baseline for the new application.
Do NOT attempt to port all legacy VB.NET screens.

Legacy source:
Treat the existing VB.NET project as read-only reference for business behavior.
Do not modify or delete legacy source files.

Target stack:
- Python / Django
- PostgreSQL
- Django Templates + HTMX + Alpine.js
- Django REST Framework only where justified
- Celery + Redis
- pytest / pytest-django
- Docker Compose

Work package scope:
1. Create the Django project under a new `web/` directory.
2. Add environment-separated settings.
3. Configure PostgreSQL only; do not create a CSV runtime provider.
4. Add Redis/Celery skeleton.
5. Add health endpoints.
6. Add request/correlation-id middleware.
7. Add base audit event model.
8. Create empty domain apps:
   accounts, core, products, drawings, control_points, inspections,
   spc, metrology, tickets, molding, commissioning, shift_tracking,
   nonconformity, mechanism, ino, laboratory, package_meter,
   analytics, notifications.
9. Configure pytest and add smoke tests.
10. Add Docker Compose for web, worker, postgres and redis.
11. Add README with local development commands.

Constraints:
- Do not implement domain tables yet except minimum audit/platform support.
- Do not use SQLite.
- Do not store booleans or numeric values as strings.
- Do not add React/Vue.
- Do not implement authentication passwords locally yet.
- Do not copy legacy SQL Server Schema.sql.
- Keep business logic out of views and model save() overrides.

Before coding:
Inspect the legacy project structure and this master plan.
Produce a concise implementation checklist in the task response, then implement it.

Definition of Done:
- docker compose config is valid
- Django starts against PostgreSQL
- migrations run cleanly
- Celery worker can start
- health endpoints pass
- pytest passes
- no legacy source modified
```

WP-001 tamamlandıktan sonra kod review yapılmalı; ancak sonra WP-002 Accounts/Permissions verilmelidir.

---

# 40. Codex Work Package Sırası

- WP-001 Platform skeleton
- WP-002 Accounts/SSO/permissions
- WP-003 Product/Mold masters
- WP-004 Drawing/revision/storage
- WP-005 PDF.js viewer
- WP-006 Control point/versioning
- WP-007 Inspection core
- WP-008 Measurement UI/history/correction
- WP-009 Legacy core migration importer
- WP-010 SPC
- WP-011 Metrology/MSA
- WP-012 Mold binding
- WP-013 Tickets
- WP-014 Connection plan Excel import
- WP-015 Shift tracking
- WP-016 Mechanism
- WP-017 Nonconformity/error report
- WP-018 INO
- WP-019 Test catalog/group
- WP-020 Test request state machine
- WP-021 Package meter
- WP-022 Commissioning
- WP-023 Notifications
- WP-024 Dashboards
- WP-025 Full legacy migration
- WP-026 UAT/performance/security hardening

Her WP ayrı review edilebilir büyüklükte tutulmalıdır.

---

# 41. Definition of Done — Sistem Seviyesi

Web dönüşümü “tamamlandı” sayılmadan önce:

- tüm aktif legacy iş süreçlerinin karşılığı bulunmalı,
- role/scope regression testleri geçmeli,
- source vs migrated row counts reconcile edilmeli,
- drawing/attachment file hashleri doğrulanmalı,
- old measurement history değişmeden görüntülenebilmeli,
- historical control point versions korunmalı,
- critical actions auditlenmeli,
- concurrent işlemlerde duplicate/lost-update oluşmamalı,
- backup restore testi yapılmalı,
- production monitoring hazır olmalı,
- desktop application yeni kayıt üretmeyi bırakmalı.

---

# 42. En Büyük Riskler

## Risk 1 — Legacy davranışın form kodlarına dağılmış olması

Çözüm: Faz 0 business rule katalogu + regression test.

## Risk 2 — SQL şemasının kaynak koddan geride olması

Kaynakta 50 CSV varken şema 24 tablo içeriyor. SQL şemasını authoritative kabul etmeyin.

## Risk 3 — DWG/AutoCAD bağımlılığı

Windows CAD worker veya sonraki faz.

## Risk 4 — Canlı CSV verisinde bozuk/legacy kayıtlar

Profiling + staging + reject report.

## Risk 5 — Rol permission ile record scope'un karıştırılması

Merkezi permission + queryset scope.

## Risk 6 — Çok büyük tek Codex promptu

Work package yaklaşımı.

## Risk 7 — Eski kayıtları yeni control point'e bağlarken tarihsel anlam kaybı

Snapshot + version mapping.

## Risk 8 — E-posta gönderimini business transaction'a bağlamak

Outbox/background job modeli.

---

# 43. v1 İçin Mimari Kararlar

Bu planın v1 varsayımları:

1. **Django tercih edildi.**
2. **Frontend SPA olmayacak; HTMX ağırlıklı olacak.**
3. **PostgreSQL şeması sıfırdan normalize edilecek.**
4. **SQL Server ara migration yapılmayacak.**
5. **Dosyalar PostgreSQL BLOB olarak tutulmayacak.**
6. **AD/Authentik kimlik doğrulama hedeflenecek.**
7. **PDF viewer PDF.js olacak.**
8. **DWG ayrı worker problemi olarak ele alınacak.**
9. **Audit birinci sınıf özellik olacak.**
10. **Legacy CSV import tek seferlik/tekrarlanabilir migration aracı olacak; yeni sistem CSV ile çalışmayacak.**
11. **Workflows service + transaction + test yaklaşımıyla yazılacak.**
12. **Mevcut uygulama kodu davranış referansı olacak, satır satır port edilmeyecek.**

---

# 44. İlk Somut Sonraki Adım

Kodlamaya başlamadan önce yapılacak ilk teknik çıktı:

**`DOMAIN_RULES.md + LEGACY_MAPPING.md + PostgreSQL ERD v1`**

Bu üçü tamamlandıktan sonra WP-001 ve WP-002 Codex'e verilmelidir.

Özellikle `LEGACY_MAPPING.md` şu formatta olmalıdır:

```text
Legacy source:
Data/ControlPoints.csv

Legacy key:
TrCode + DrawingRev + DrawingScope + MeasureId + MeasureVersion

Target:
control_points_controlpoint
control_points_controlpointversion

Transform:
LowerTol positive -> negative normalized
XPercent / 100 -> x_ratio
YPercent / 100 -> y_ratio
YES/NO -> boolean
ValidFrom/ValidTo -> timestamptz

Validation:
No duplicate active version
Numeric parse failures -> migration reject report
```

Bu belge Codex'in yanlış veri modeli icat etmesini ciddi ölçüde azaltır.

---

# Sonuç

Proje teknik olarak yapılabilir ve web'e dönüşüm için mevcut kaynak kod oldukça değerli bir başlangıç noktasıdır. En büyük iş Python/Django kodunu yazmak değil, mevcut uygulamada oluşmuş iş kurallarını sistematik biçimde yeni domain modeline aktarmaktır.

En güvenli yaklaşım:

```text
Legacy uygulamayı analiz et
        ↓
Business rule + mapping dokümanını kilitle
        ↓
PostgreSQL domain modelini kur
        ↓
Django çekirdeği geliştir
        ↓
Modül modül Codex work package
        ↓
Regression/UAT
        ↓
İki migration rehearsal
        ↓
Production cutover
```

Bu master plan Codex'e doğrudan “uygulamayı tamamen dönüştür” komutu vermek yerine, kontrollü ve test edilebilir küçük iş paketleri halinde geliştirme yaptırmak üzere hazırlanmıştır.
