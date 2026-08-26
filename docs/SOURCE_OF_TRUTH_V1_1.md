# A Blok Kalite Kontrol — Source of Truth v1.1

**Tarih:** 26.08.2026  
**Durum:** WP-001 için onaylı mimari karar katmanı  
**Amaç:** `CODEX_ANALYSIS_V1.md` incelemesinde bulunan blocker ve çelişkileri çözmek; v1 dokümanlarını yeniden yazmadan hangi hükümlerin geçerli olduğunu kesinleştirmek.

---

## 1. Doküman önceliği

Bir çelişki olduğunda aşağıdaki sıra uygulanır:

1. `SOURCE_OF_TRUTH_V1_1.md` — bu dokümandaki açık kararlar ve override'lar
2. `DOMAIN_RULES_V1.md` içindeki doğrulanmış **AS-IS** davranışlar
3. Onaylanmış ADR / **TO-BE** kararları
4. `POSTGRESQL_ERD_V1.md`
5. `LEGACY_MAPPING_V1.md`
6. `WEB_DONUSUM_MASTER_PLANI_V1.md`
7. `CODEX_ANALYSIS_V1.md` — review/gap-analysis kaynağıdır; tek başına ürün kararı değildir
8. Legacy kaynak kod — AS-IS doğrulama kaynağıdır; hedef mimariyi belirlemez

Codex belirsizliği kendi başına iş kuralına dönüştürmemelidir. Açık olmayan TO-BE davranışı ilgili ADR/work package kapısına bırakılmalıdır.

---

## 2. WP-001 artık READY

`CODEX_ANALYSIS_V1.md` içindeki `READY_FOR_WP001 = NO` sonucu, eski WP-001 tanımındaki kapsam çelişkileri için geçerlidir. Bu v1.1 ile WP-001 daraltılmış ve geri dönüşü pahalı kararlar açıkça belirlenmiştir.

**Yeni durum:** `READY_FOR_WP001 = YES`, ancak yalnız bu dokümandaki daraltılmış WP-001 kapsamı için.

Canlı CSV profili, drawing identity, binding cardinality, SPC correction, commissioning state veya gerçek kalite-domain constraint'leri WP-001 kapsamına alınmayacaktır.

---

## 3. WP-001 kapsam kararı

WP-001 yalnız **platform skeleton + ilk migration güvenliği** işidir.

### WP-001 içinde oluşturulacaklar

- Django project/config yapısı
- PostgreSQL bağlantısı
- Redis ve Celery skeleton
- Nginx reverse proxy skeleton
- environment-separated settings
- structured logging + correlation/request id
- `/health/live` ve `/health/ready`
- pytest / pytest-django
- minimal `accounts.User` custom user modeli
- minimal append-only `audit.AuditEvent` platform modeli
- Docker Compose
- development/CI komutları ve README

### WP-001 içinde oluşturulmayacaklar

- Product/Mold tabloları
- Drawing/Revision tabloları
- Control Point tabloları
- Inspection/Measurement tabloları
- Ticket, binding, SPC, MSA, laboratory vb. domain tabloları
- legacy CSV importu
- drawing decrypt/import
- dosya upload/download endpointleri
- Authentik entegrasyonunun tamamı
- role/permission business matrisi
- domain state machine'leri
- email/outbox domain akışları
- PDF.js/CAD işleri

**Override:** `POSTGRESQL_ERD_V1.md` bölümündeki “İlk Django migration seti” ifadesi WP-001 için geçerli değildir. Oradaki sıra WP-002–WP-004 migration wave planı olarak okunmalıdır.

---

## 4. Teknoloji baseline

Yeni proje için baseline:

- **Python:** 3.13.x
- **Django:** 5.2 LTS serisi; lock dosyasında güncel 5.2.x patch release
- **PostgreSQL:** 18.x
- **Redis:** supported stable image; exact image digest/patch lock dosyası veya deployment manifestinde sabitlenir
- **Celery:** Django 5.2/Python 3.13 ile uyumlu supported stable release; dependency lock'ta pinlenir
- **Frontend:** Django Templates + HTMX + Alpine.js
- **API:** DRF yalnız gerçek API ihtiyacı olduğunda
- **Test:** pytest + pytest-django

Yeni projede SQLite runtime/test backend kullanılmayacaktır. Testler de PostgreSQL üzerinde çalışabilmelidir.

---

## 5. ADR-007 — Authentication / identity

**Karar: ACCEPTED**

Production hedefi **Authentik üzerinden OIDC**, Authentik'in kurumsal dizin/AD ile entegre olmasıdır.

Kurallar:

- Django'da custom user model ilk migration'dan önce oluşturulur.
- `AUTH_USER_MODEL` WP-001'de sabitlenir.
- Legacy PBKDF2/hash/salt/plain/protected parola verileri hiçbir hedef tabloya taşınmaz.
- INO içindeki ikinci credential store da migrate edilmez.
- Production'da local username/password login varsayılan olarak kapalıdır.
- OIDC kullanıcısına gerektiğinde `set_unusable_password()` uygulanır.
- Historical actor snapshot ile canlı login identity birbirinden ayrıdır.
- OIDC `issuer + subject` kimliği WP-002'de kalıcı external identity modeliyle bağlanır.
- Development/test ortamı için OIDC bağımlılığı olmadan test user factory/management command kullanılabilir; bu production authentication yolu değildir.

### WP-001 minimal User

Custom user modelin amacı gelecekte değiştirilmesi pahalı olan `AUTH_USER_MODEL` kararını baştan vermektir. WP-001'de domain rol/scope modeli uygulanmaz.

Önerilen temel özellikler:

- UUID primary key
- `username`/display identity alanları Django admin/dev ihtiyacına uygun
- email nullable/normalize edilebilir
- active/staff/superuser teknik alanları
- created/updated timestamps

OIDC-specific identity tabloları WP-002'de eklenir.

---

## 6. ADR-011 — Zaman politikası

**Karar: ACCEPTED**

- Django `TIME_ZONE = "Europe/Istanbul"`
- Django `USE_TZ = True`
- PostgreSQL olay zamanları `timestamptz`
- Uygulama içi datetime değerleri timezone-aware olmalıdır.
- DB/worker/container saatleri UTC olabilir; kullanıcı gösterimi `Europe/Istanbul` üzerinden yapılır.
- Legacy açıkça UTC işaretli alanlar UTC parse edilir.
- Legacy timezone bilgisi olmayan tarih/saatler varsayılan olarak `Europe/Istanbul` yerel zamanı kabul edilir.
- Legacy parse sırasında **raw string + parse strategy + parsed timestamp** staging/reconciliation kaydında korunur.
- Sabit `UTC+03:00` kullanılmaz; `Europe/Istanbul` zone database kullanılır.

---

## 7. ADR-018 — Numeric/rounding baseline

**Durum: PARTIALLY ACCEPTED; domain rounding ayrıntısı WP-006/WP-007 öncesi parity test ile kapanacak**

Şimdiden geçerli kurallar:

- Python domain hesaplarında `float` ile kalite limiti hesabı yapılmaz; `Decimal` kullanılır.
- PostgreSQL ölçüm/tolerans hedef tipi temel olarak `numeric(14,5)` olacaktır; domain profiling daha fazla hassasiyet gerektirirse ADR güncellenir.
- Legacy raw numeric text staging'de kaybedilmez.
- Türkçe decimal comma ve invariant decimal point kontrollü parser ile ele alınır.
- DB'ye yazmadan önce sessiz locale-dependent dönüşüm yapılmaz.

Henüz karara bağlanmayanlar:

- display rounding precision
- comparison öncesi rounding uygulanıp uygulanmaması
- unit conversion politikası
- boundary equality parity ayrıntıları

Bunlar WP-006/WP-007 öncesinde legacy golden fixture ile doğrulanacaktır. Bu nedenle WP-001 blocker değildir.

---

## 8. Canlı veri discovery kararı

`CODEX_ANALYSIS_V1.md` doğru olarak canlı authoritative veri olmadan unique key/duplicate/orphan varsayımlarının doğrulanamayacağını belirtir.

**Karar:** Bu çalışma WP-001 blocker değildir; fakat **WP-003 Product/Mold ve WP-004 Drawing/Revision domain constraintleri başlamadan önce zorunludur.**

Yeni ara iş paketi:

### WP-000 — Legacy Data Profiler

Read-only profiler aşağıdaki çıktıları üretmelidir:

- file/header manifest
- byte size + SHA-256
- encoding tahmini/kanıtı
- row counts
- null/blank dağılımı
- candidate business key duplicate raporu
- orphan/reference raporu
- date format/timezone samples
- numeric format samples
- multi-value/delimiter samples
- drawing file V1/V2/encrypted/plain counts
- missing files / duplicate filename / duplicate hash
- SQL Server kopyası varsa CSV ile karşılaştırma, fakat double-import yapmama

Profiler **veri değiştirmeyecek** ve credential/secret değerleri rapora yazmayacaktır.

---

## 9. ADR-001 — Drawing identity

**Durum: DEFERRED — WP-000 + WP-003/WP-004 gate**

Şimdilik `(normalized TR, scope)` için DB unique constraint yazılmaz.

Önce canlı veri üzerinde:

- case
- whitespace
- tire/ayraç
- leading zero
- aynı TR'nin farklı scope/product/mold ilişkileri

profil edilir.

Canonical normalization ve merge/split yetkisi ADR-001'in final kararında tanımlanır.

---

## 10. ADR-008 — Authorization scope

**Durum: WP-002 gate**

WP-001 yalnız Django teknik admin/superuser yetkilerini kullanabilir. Production domain permission davranışı uygulanmayacaktır.

WP-002 başlamadan önce tek permission baseline oluşturulmalıdır:

- action permission
- department scope
- drawing scope
- ownership scope
- row scope
- field scope (özellikle INO)
- “Yönetici” read-only istisnaları

UI gizleme authorization sayılmaz; service/query seviyesinde server-side enforcement zorunludur.

---

## 11. ADR-009 — File storage / encryption

**Karar: WP-001 için ACCEPTED BASELINE**

- DB içinde drawing/file binary saklanmaz.
- Django Storage API arkasında abstraction kullanılır.
- Development ve ilk deployment için persistent filesystem storage kullanılabilir.
- Storage key kullanıcı kontrollü filesystem path olmayacaktır.
- MinIO/S3-compatible backend daha sonra aynı abstraction üzerinden eklenebilir.
- WP-001 upload/download domain endpointi geliştirmez.
- Legacy V1/V2 drawing decrypt işlemi yalnız kontrollü migration worker/runbook ile yapılır; web request processi legacy decrypt key taşımaz.
- Malware scan/quarantine policy WP-004 öncesi finalize edilir.

---

## 12. ADR-010 — DWG stratejisi

**Durum: DEFERRED — Drawing/CAD work package gate**

Önerilen yön ayrı düşük yetkili **Windows CAD Worker + AutoCAD Core Console** trust boundary'sidir. Django web processine AutoCAD bağımlılığı kurulmayacaktır.

DXF için Python/`ezdxf` yolu ayrıca değerlendirilebilir. WP-001 kapsamında CAD yoktur.

---

## 13. ADR-013 — Audit / transition history / outbox

**Karar: ACCEPTED ARCHITECTURAL DIRECTION**

- Audit append-only olacaktır.
- Kritik domain transition'ları service/use-case katmanında `transaction.atomic()` ile yürütülecektir.
- Email/Celery işi transaction içinde dış sisteme gönderilmeyecektir.
- Notification gerektiren domainlerde transactional outbox kullanılacaktır.
- Aggregate-specific state history tabloları ilgili work package içinde eklenecektir.

WP-001'de yalnız generic platform audit modeli oluşturulur; notification outbox tablosu ilk gerçek notification üreten domain work package'ında eklenebilir.

---

## 14. TO-BE karar sahipliği ve bekleyen domain ADR'leri

Aşağıdaki konular legacy AS-IS değildir ve Codex kendisi karar vermemelidir:

| ADR | Mevcut güvenli davranış | Final karar kapısı |
|---|---|---|
| ADR-002 aktif binding cardinality | AS-IS: uyarı/teyit, hard block değil | WP-012 |
| ADR-003 makine değişim açıklaması | AS-IS: hard-required değil | WP-012 |
| ADR-004 `ŞARTLI ONAY` | AS-IS: commissioning tamamlanmış sayılmaz | WP-022 |
| ADR-005 SPC historical correction | AS-IS korunur; yeni immutable yaklaşım henüz uygulanmaz | WP-010 |
| ADR-006 completed record correction | Legacy fiziksel edit/delete yolları otomatik kopyalanmaz | WP-008/WP-020/WP-021 |
| ADR-012 product–mold temporal relation | unique/validity constraint yok | WP-000 + WP-003 |
| ADR-014 retention/legal hold | purge uygulanmaz | production readiness |
| ADR-015 INO target schema | credential store birleşir; typed domain profiling bekler | INO WP |
| ADR-016 cutover | CSV/files authoritative aday; SQL snapshot double-import edilmez | migration readiness |
| ADR-017 HA/RPO/RTO | local/PoC Compose hazır; production HA ayrı karar | production deployment |

Bir ADR sonuçlanana kadar legacy davranışı değiştiren hard DB constraint eklenmemelidir.

---

## 15. Corrected migration waves

### Wave 0 — WP-001

- Django framework/system migrations
- `accounts.User` minimal custom user
- `audit.AuditEvent` minimal platform audit

### Wave 1 — WP-002

- OIDC external identity
- roles / permissions / scope model
- actor snapshot helpers

### Wave 2 — WP-003

- core master data gerekenleri
- product/mold/product-mold
- yalnız WP-000 profiling ile doğrulanmış constraints

### Wave 3 — WP-004/WP-006

- drawing/revision/file metadata
- control point logical/version/group area
- drawing identity ADR tamamlandıktan sonra uniqueness

### Sonraki wave'ler

Inspection, molding, tickets, SPC, metrology ve diğer domainler kendi work package'larında migration üretir.

---

## 16. WP-001 Definition of Done v1.1

WP-001 ancak aşağıdakilerin tamamında DONE sayılır:

1. `legacy/` altında değişiklik yok.
2. Python 3.13 + Django 5.2 LTS project çalışıyor.
3. PostgreSQL dışında runtime DB backend yok.
4. `AUTH_USER_MODEL` ilk migration'dan itibaren custom model.
5. `docker compose up` ile web/postgres/redis/worker (ve yapılandırılmışsa nginx) ayağa kalkıyor.
6. `manage.py migrate` temiz DB üzerinde başarılı.
7. `/health/live` dependency'siz 200 döndürüyor.
8. `/health/ready` en az DB ve Redis erişimini doğruluyor.
9. Celery worker uygulamayı import edip başlayabiliyor.
10. Structured log ve correlation id en az request/response hattında test edilmiş.
11. Audit modelinde uygulama API'si üzerinden update/delete yolu yok.
12. pytest PostgreSQL üzerinde geçiyor.
13. Secret'lar repository'ye yazılmamış.
14. SQLite bağımlılığı/test fallback'i yok.
15. Domain app/model/state-machine oluşturulmamış.
16. `docs/WP001_IMPLEMENTATION_REPORT.md` oluşturulmuş; komut/test sonuçları ve değişen dosya özeti var.
17. `git diff -- legacy/` boş.

---

## 17. WP-001 sonrası sıra

1. WP-001 code review
2. WP-000 Legacy Data Profiler (WP-003/004 öncesi zorunlu)
3. WP-002 Accounts/OIDC/Permissions
4. Permission baseline review
5. WP-003 Product/Mold
6. ADR-001 Drawing identity finalize
7. WP-004 Drawing/Revision/Storage

WP-000 ile WP-002, WP-001 tamamlandıktan sonra paralel ilerleyebilir; ancak WP-003/WP-004, profiler çıktısı ve ilgili ADR'ler kapanmadan başlamaz.
