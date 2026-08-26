ihancioglu@baylan-portainer:~/uretim-portal$ cat docs/CODEX_ANALYSIS_V1.md
# A Blok Kalite Kontrol — Codex Analizi v1

**Tarih:** 26.08.2026
**Kapsam:** Statik repository/doküman/VB.NET kaynak kod analizi
**Değişiklik türü:** Yalnız analiz; uygulama kodu, Django modeli, migration ve legacy kaynak değişikliği yoktur.

## 0. Okuma yöntemi ve sınırlar

Dokümanlar istenen sırada (`WEB_DONUSUM_MASTER_PLANI_V1.md`, `DOMAIN_RULES_V1.md`, `LEGACY_MAPPING_V1.md`, `POSTGRESQL_ERD_V1.md`) okunmuş; ardından `legacy/` altındaki solution/proje, 64 `Frm*` formu, model, servis, INO modülü, SQL hazırlık katmanı, README ve yetki matrisi statik olarak taranmıştır. Çelişkilerde `DOMAIN_RULES_V1.md` içindeki **AS-IS > LEGACY-COMPAT > onaylanmış TO-BE** önceliği esas alınmıştır. Bu raporda “mevcut” yalnız kaynakta görülen davranışı, “öneri” ise onay gerektiren hedef davranışı anlatır.

Repository canlı `Data/` dizinini ve authoritative CSV/binary örneğini içermiyor. Bu nedenle kayıt sayısı, gerçek duplicate/null/orphan oranı, encoding, timestamp dağılımı, dosya-decrypt başarısı ve iş anahtarı tekilliği doğrulanamamıştır. Kaynakta varlığı görülen bir kod yolunun üretimde kullanıldığı da telemetri/canlı veri olmadan kesin kabul edilmemelidir.

## 1. Repository ve legacy uygulama özeti

- Repository şu anda analiz ve legacy teslimat deposudur; Django projesi yoktur. `legacy/ABlokKaliteKontrol.sln`, ana `TeknikResimOlcum` WinForms projesi ve ayrı launcher içerir.
- Ana uygulama 64 form sınıfı ve merkezi, yaklaşık 5.300 satırlık `Services/DataService.vb` etrafında kuruludur. Formlar yalnız UI değildir: validation, state türetme, permission ve hesaplama kuralları da form event/methodlarına dağılmıştır. Örnekler: `FrmMeasurementEntry.Save_Click`, `FrmPlasticShiftErrorReport.ValidateWorkflow`, `FrmNewMoldCommissioningDetail.DetermineStage`, `FrmProductionTicketEntry.StartBinding_Click` ve `FrmTestRequestDetail` action akışları.
- Operasyonel authoritative katman fiilen CSV + binary/shared filesystem’dir. `CsvUtil` kilit, retry, atomik replace, backup ve recovery sağlar; `CriticalDataJournalService` ve `DataService.RecoverPendingTransactions` çok dosyalı işlemlerde crash recovery yaklaşımı uygular.
- `Sql/Schema.sql`, `DatabaseConfig` ve `SqlDatabaseService` bir SQL Server hazırlık/import katmanıdır. `ApplicationLifecycleService` SQL modunda şemayı kontrol edebilir; ancak legacy `README_TR.md` açıkça ekranların halen CSV kullandığını söyler. Bu şema hedef PostgreSQL modeli veya eksiksiz AS-IS envanteri değildir.
- Teknik resimler filesystem’de şifreli tutulur. `CryptoService.DecryptDrawing`, V1 CBC/HMAC uyumluluğu ile V2 AES-GCM formatını destekler; `AppPaths.ResolveDrawingFilePath` kök dışına çıkmayı engeller. PDF/DXF render ve DWG için AutoCAD Core Console entegrasyonu Windows/desktop bağımlılıkları taşır.
- Yetkilendirme `AppState` computed `Can*` özellikleri, `AuthorizationService`, form `ApplyAccess/ApplyPermissions` kodu ve `YETKI_MATRISI.*` arasında dağınıktır. INO’nun ayrıca `Modules/INO/Services/UserStore.vb` içinde kendi kullanıcı/parola alanı vardır; bu ikinci identity store hedefte birleştirilmelidir.
- Deployment updater/launcher, active sessions, running instances, inactivity warning, local drafts, WebView2 ve Outlook draft üretimi web hedefinin domain parçası değildir; fakat cutover/operasyon tasarımında karşılıkları açıkça seçilmelidir.

## 2. Tespit edilen domain modülleri

| Domain | Legacy kanıtı (örnek sınıf/metot) | Hedef sınır notu |
|---|---|---|
| Kimlik, kullanıcı, rol, session | `UserService.Authenticate`, `AuthorizationService`, `FrmLogin`, `FrmUserManagement`, `DataService.EnsureCurrentUserSession` | `accounts`; authentication ile authorization ayrılmalı |
| Ürün, kalıp, teknik resim/revizyon | `DataService.SaveProduct/DeleteProduct`, `FrmProductAdmin`, `ProductNameResolver` | `products`, `drawings`, `core_file_object` |
| Dosya/crypto/CAD | `CryptoService`, `AppPaths.ResolveDrawingFilePath`, `PdfRenderService`, `DxfDimensionImportService`, `AutoCadDimensionImportService` | storage servisi; CAD worker ayrı trust boundary |
| Kontrol noktası/grup alanı | `DataService.SaveControlPoint`, `ReviseControlPoint`, `SaveMeasurementGroupArea`; `FrmControlPointAdmin` | logical point ile version ayrımı korunmalı |
| Ölçüm/görsel kontrol/göz | `FrmMeasurementEntry`, `DataService.AppendMeasurement/AppendVisualControl/AppendClosedEye`, `MeasurementDraftService` | session-eye-measurement aggregate |
| Ölçüm geçmişi/düzeltme | `FrmMeasurementHistory.DeleteSelectedRecord_Click`, `FrmMeasurementCorrection.SaveClick`, `DataService.CorrectMeasurementValue/DeleteMeasurementRecord` | append-only correction/cancel politikası gerekli |
| SPC/istatistik | `FrmSpcDashboard.BuildSeries/BuildRiskSummary`, `FrmSpcAnalysis.BuildSubgroups`, `DataService.CorrectSpcHistoricalLimits` | hesap standardı ve historical correction semantiği ADR |
| Metrology/MSA | `FrmMsaDashboard.Save_Click`, `DataService.SaveMeasurementDevice` | cihaz master + kalibrasyon tarihi |
| Kalıp bağlama/plan | `FrmProductionTicketEntry`, `DataService.CompleteMoldBindingAndCreateProductionTicket`, `FrmMoldConnectionPlan` | `molding`; concurrency invariantları açık karar |
| Ticketlar | `DataService` production/quality-production/mold ticket metotları; `FrmQualityTickets`, `FrmMoldTicketDetail` | üç farklı aggregate/state machine |
| Plastik/mekanizma vardiya | `SavePlasticShiftTrackingRecord`, `SaveMechanismShiftTrackingRecord`, `ShiftTrackingPhotoService` | ortak çekirdek, ayrı scope/izin |
| Uygunsuzluk/hata raporu | `FrmPlasticShiftErrorReport.ValidateWorkflow`, `DataService.SavePlasticShiftErrorReport/Save...Evaluation` | parent/action/review/evaluation olarak normalize |
| Mekanizma kalite | `CompleteMechanismQualityControl`, `FrmMechanismQualityControlDetail` | delivery/control aggregate |
| INO-1/INO-2 | `Modules/INO/Forms/MainForm.BuildTableSchema`, `ResolveStatus`, `RowEditForm.ApplyReadOnlyStyle` | dinamik CSV kolonları ve field-level ACL nedeniyle bağımsız keşif |
| Laboratuvar/test | `Create/Accept/Complete/CancelTestRequest`, `Complete/Skip/ReopenTestRequestStep`, katalog/grup servisleri | request + immutable step snapshot |
| Paket sayaç | `SavePackageMeterControl`, `EvaluatePackageMeterReferenceFlows`, `TryResolvePackageMeterRange` | control-line aggregate |
| Yeni kalıp devreye alma | `FrmNewMoldCommissioningDetail.DetermineStage/ApplyApproval` | checklist/trial/action/approval child’ları |
| Bildirim/e-posta | `*EmailNotificationService`, `OutlookEmailDraftService`, recipient formları | outbox/idempotent async notification |
| Audit/health/recovery | `AuditService`, `CriticalDataJournalService`, `FrmDataHealth`, `ErrorLogService` | append-only audit + observability + import rejects |
| Analytics/dashboard | SPC, scrap, rework ve binding dashboard sınıfları | read model/reporting; transactional modellere gömülmemeli |

## 3. Dokümanlar ile kaynak kod arasındaki tutarsızlıklar

### 3.1 Kesin ayrımlar

1. **Test request canonical state:** Master Plan örneği `NEW -> PROCESSING -> COMPLETED` derken DOMAIN_RULES AS-IS `OPEN -> ACCEPTED -> COMPLETED` der. Kaynak `DataService.AcceptTestRequest`, `CompleteTestRequest`, `CancelTestRequest` ikinci sözlüğü kullanır. Hedef enum için DOMAIN_RULES esas alınmalıdır.
2. **Tek aktif binding:** Master Plan bunu kritik kural/constraint gibi sunar. Kaynak `FrmProductionTicketEntry` başlangıç akışı aynı kalıp için birden çok `STARTED` kayda izin verir ve kullanıcıya uyarı/teyit uygular; DOMAIN_RULES bunu doğru biçimde AS-IS + TO-BE adayı olarak düzeltir. ADR onayı olmadan partial unique eklenemez.
3. **Makine değişim nedeni:** Master Plan zorunlu olmasını önerir. Kaynakta `FrmProductionTicketEntry` reason=`MAKİNE DEĞİŞİMİ` için ayrı açıklamayı hard-required yapmaz; dashboard `IsMachineChangeRow` ile sınıflandırır. Bu bir TO-BE kararıdır.
4. **Commissioning şartlı onay:** Önerilen Master Plan state machine `CONDITIONALLY_APPROVED` terminaline açıktır; `FrmNewMoldCommissioningDetail.DetermineStage` yalnız üç onay ve final `ONAYLANDI` olduğunda `TAMAMLANDI` türetir. `ŞARTLI ONAY` AS-IS’te tamamlanmış değildir.
5. **SQL Server şemasının otoritesi:** `SqlDatabaseService.ImportAllCsvToSqlFromConfig` CSV tablolarını bulk-replace eder ve `Schema.sql` yalnız hazırlık şemasıdır; `DataService` ekran operasyonlarını CSV’de sürdürür. ERD’nin bunu hedef şema kabul etmemesi doğrudur; migration discovery sırasında SQL kopyası varsa authoritative sanılmamalıdır.
6. **Silme/immutability farkı:** ERD completed ölçüm, ticket ve paket verisi için correction/soft-delete önerir; legacy `DataService.DeleteMeasurementRecord`, `DeleteMoldTicket`, `DeleteTestRequest`, `DeletePackageMeterControl` ve ilgili form delete eventleri fiziksel silme yolları içerir. Bunlar sessizce “AS-IS korunuyor” diye yorumlanamaz; retention ve yetkili correction kararı gerekir.
7. **INO identity:** Ana dokümanlar lokal parolanın taşınmayacağını söyler; kaynakta ana `UserService` yanında bağımsız `Modules/INO/Services/UserStore.ValidatePassword/CreateOrUpdateUser` vardır. Credential taşımama kararı iki store’u da açıkça kapsamalıdır; mapping yalnız `Users.csv` ile sınırlı kalmamalıdır.
8. **Zaman semantiği:** ERD `timestamptz` ister. Legacy kodun büyük kısmı `DateTime.Now.ToString(...)`, bazı journal alanları ise açıkça UTC (`CreatedAtUtc`) kullanır. Bunları tek varsayılan timezone ile kör parse etmek olay sırasını değiştirebilir.
9. **Drawing identity:** `DataService.GetControlPoints` scope’u optional alır; eski satırlarda scope boş olabilir. ERD `(normalized TR, scope)` provisional unique’i canlı ürün/scope çakışması görülmeden kesinleştirilemez.
10. **Measurement parent:** Legacy `AppendMeasurement` her göz/record için flat satır yazar; multi-eye parent session ID yoktur. ERD’deki session merge hedef modeldir, kaynak davranış değildir. Mapping’in “RecordId başına session” güvenli varsayımı korunmalıdır.

### 3.2 Dokümanların kaynakla uyumlu kritik düzeltmeleri

- Pozitif girilen lower tolerance’ın negatif yönde normalize edilmesi ve limit hesabı `FrmControlPointAdmin`/`DataService` davranışıyla uyumludur; import raw değerini ayrıca tutmak gerekir.
- `DataService.SplitMoldCodeTokens/MoldCodeMatches` kalıp ilişkisinin salt TR değil, tokenlaştırılmış mold code semantiği taşıdığını doğrular.
- Test step strict sıra, skip/reopen override, 2.000 karakter açıklama limiti ve request snapshot yaklaşımı `DataService.CompleteTestRequestStep`, `SkipTestRequestStep`, `ReopenTestRequestStep` ile uyumludur.
- Dosya path traversal savunması `AppPaths.ResolveDrawingFilePath`; authenticated encryption ise `CryptoService.DecryptV2Bytes` içinde vardır. Web sürümünde eşdeğer koruma gerekir, desktop implementasyonu taşınmaz.

## 4. Eksik veya netleştirilmesi gereken iş kuralları

1. **Drawing/product kimliği:** TR case, tire, whitespace, baştaki sıfır ve aynı TR’nin birden çok ürün/mold/scope ile kullanımında canonical identity ve merge/split yetkisi.
2. **Revision sıralaması:** `DrawingRev` serbest metin olduğunda `A`, `01`, `R2`, boş değerlerin sırası; activation için approval rolü, effective time ve geri alma kuralı.
3. **Inspection lifecycle:** DRAFT/IN_PROGRESS/COMPLETED/CANCELLED geçişleri, timeout/abandon, aynı production ticket’tan paralel session, göz sayısını sonradan değiştirme ve partial visual completion politikası.
4. **Rounding/precision:** Ölçüm input scale, limit karşılaştırmasından önce/sonra rounding, boundary equality, birim dönüşümü ve locale decimal separator. `NumberUtil` parsing davranışı hedef DB precision’ı tek başına belirlemez.
5. **Overall result:** closed eye, eksik optional ölçü, visual NOK ve numeric NOK birlikteyken session/ticket sonucunun deterministik precedence’ı.
6. **Correction/cancellation:** Completed session, package control, NCR, test ve ticket için kim reopen/correct/cancel edebilir; eski raporların yeniden hesaplanıp hesaplanmayacağı; correction’ın bildirim üretip üretmeyeceği.
7. **Ticket idempotence ve bağ:** Tek inspection session’dan kaç ticket; ticket kapanınca bağlı inspection değişebilir mi; production ticket otomatik kapanmasının kesin koşulları.
8. **Binding cardinality:** Tek aktif kayıt kalıp başına mı, mold token başına mı, makine başına mı; boş/çoklu MoldCode davranışı ve confirmation yerine hard block kararı.
9. **NCR workflow:** Üç evaluator pozisyonunun değişmez katalog anahtarı, assignment değişince pending evaluation sahipliği, reject sonrası reopen, due-date/SLA ve imza anlamı.
10. **Laboratuvar:** Request reason’ın çoklu yapısı, department master eşlemesi, due-date timezone, attachment silme/version, step result vocabulary ve completed request correction.
11. **Paket sayaç:** Range katalog versiyonlama, 500 sınırına concurrent append, duplicate serial’ın global mi control-local mı olduğu, draft retention ve Admin düzeltme yöntemi.
12. **Commissioning:** `ŞARTLI ONAY`ın hedef sonucu, stage’e geri dönüşler, approval revoke, child silme, trial number tipi ve checklist template versiyonu.
13. **Metrology:** Due-date inclusive/exclusive, expired cihazla ölçüm bloklanır mı, calibration history tek satır mı event tablosu mu ve `reference_device` self-FK semantiği.
14. **Bildirim:** Domain transaction başarısızken mail davranışı, retry/backoff, recipient snapshot, kişisel veri retention ve event key üretimi.
15. **Yetki scope’u:** Rol permission’a ek olarak department, drawing scope, ownership ve row/field scope’un kombinasyon/öncelik kuralları; “Yönetici” salt-okunur istisnaları.
16. **Audit/retention:** Legal retention süreleri, export/erasure, hassas açıklama/photo erişimi, break-glass işlemi ve audit event düzeltme yasağı.

## 5. ERD'de eksik veya riskli ilişkiler

| Konu | Risk | Öneri / karar kapısı |
|---|---|---|
| Department ilişkileri | Birçok tabloda department `varchar/snapshot` kalırsa scope ve raporlama parçalanır | Aktif master için nullable FK + zorunlu snapshot; historical/deleted değer korunmalı |
| Product–mold zaman boyutu | `product_mold` ilişkisinde geçerlilik/revizyon yok | `valid_from/to`, provenance ve uniqueness kararı |
| Inspection source | Session yalnız optional production ticket/commissioning ile yetinirse kaynak türleri çoğaldıkça nullable FK kümelenir | Explicit source type + constrained source relation veya ayrı link tabloları ADR’si |
| Measurement correction | Revision yalnız old/new value tutarsa limit/result/snapshot düzeltmeleri temsil edilemez | Değiştirilebilir alan kapsamı ve full before/after snapshot belirlenmeli |
| State history | Ticket, binding, test request, NCR, commissioning için yalnız current status olay geçmişini kaybettirir | Ortak olmayan, aggregate’e özgü transition/event history tabloları |
| SPC correction | `spc_key` text relation orphan/çakışma üretir | Logical control point FK + key/version snapshot; etki kümesi ilişki tablosu veya immutable manifest |
| Metrology calibration | `metrology_device` üzerinde yalnız son kalibrasyon tarihleri tarihçe sağlamaz | `metrology_calibration_event/certificate_file` ilişkisi değerlendirilmeli |
| File ownership | Generic `core_file_object` referans sayımı/orphan ve ACL problemi taşır | Attachment/drawing linkleri üzerinden object-level auth, quarantine/scan state, retention hold |
| Notification entity | Generic `entity_type/entity_id` DB referential integrity sağlamaz | Outbox event payload + aggregate-specific optional link stratejisi; silme davranışı |
| Audit entity | Generic reference doğal olarak FK’sizdir | Snapshot zorunlu; partition/retention ve actor `SET_NULL` politikası |
| NCR evaluator assignment | Assignment current routing iken evaluation snapshot mantığı yalnız metinle kalabilir | Evaluation üzerinde assigned-user/role/email snapshot ve assignment version FK |
| Commissioning template | Checklist item yalnız commissioning child ise template provenance kaybolur | Versioned checklist template + item snapshot opsiyonu |
| INO | Tek geniş `ino_record` dinamik/rol bazlı kolonları typed kurala bağlamıyor | Gerçek header profiling sonrası typed alt varlıklar; JSONB’yi geçici staging dışında varsayılan yapmama |
| Photos/attachments | File ile domain child arasında malware/visibility/version bilgisi eksik | scan status, uploader, classification, logical filename, delete/replace history |
| Actor modeli | `created_by FK + snapshot` her yerde tutarlı değil | Ortak actor snapshot standardı; import/system actor ve deactivated user davranışı |

Ek constraint riskleri: nullable kolonlar üzerindeki composite unique’ler PostgreSQL’de beklenen tekilliği sağlamayabilir; normalized expression/`NULLS NOT DISTINCT` stratejisi seçilmelidir. `numeric` check’leri legacy bilinmeyen/boş değerleri reject etmeden önce staging’de uygulanmalıdır. Partial unique binding ve drawing constraint’leri veri profiling/ADR’den önce migration’a girmemelidir.

## 6. Migration açısından riskli legacy veri yapıları

1. **Canlı veri yokluğu en büyük kanıt açığıdır.** Repository’de CSV/binary payload yok; dokümandaki provisional key’ler test edilemiyor.
2. **Flat measurement kayıtları:** `RecordId` parent session değildir; tarih/lot/operator yakınlığıyla merge yapılmamalıdır. Control point resolution başarısızsa snapshot korunup nullable FK/reject raporu üretilmelidir.
3. **Serbest metin sayılar/tarihler/booleanlar:** Türkçe decimal comma, kültüre bağlı `DateTime.Now` stringleri, `YES/NO`, Türkçe etiketler, boş string ve sentinel değerler typed PostgreSQL kolonlarında parse/reconciliation ister.
4. **Lower tolerance semantiği:** Pozitif legacy lower tolerance display/input normalizasyonu raw kaynağı kaybettirmeden canonical limit üretmelidir; aksi halde historical OK/NOK değişir.
5. **Kimlik çakışmaları:** TR+scope, product name+plastic code, SpcKey, MeasureId/version, DeviceId, test name, email ve INO internal ID için case/trim-aware duplicate profiling şarttır.
6. **Çok değerli stringler:** MoldCode tokenları, requested tests, test group üyeleri, product listeleri, recipient scope’ları ve action1..5 kolonları normalizasyon sırasında delimiter/escaping belirsizliği taşır.
7. **Cross-file atomiklik:** Binding+ticket, measurement+visual+closed-eye+quality ticket ve journal kayıtları ayrı CSV’lerdedir. Crash sonrası yarım transactionlar `CriticalDataJournalService` ve pending files ile reconcile edilmelidir.
8. **Fiziksel silmeler:** Kaynakta silinmiş kayıtlar için tombstone yoktur; backup/audit olmadan geçmiş FK’lerin neden eksik olduğu ayırt edilemez.
9. **Dosyalar:** Relative/absolute veya eski adlar, eksik binary, aynı ad-farklı hash, V1/V2 encrypted drawing, key erişimi, attachment boyutu ve path traversal rejectleri cutover blocker olabilir.
10. **Kullanıcılar:** AD’de olmayan actor’lar login hesabına dönüşmemeli; snapshot identity korunmalı. Ana ve INO credential store’larındaki hash/salt/plain/protected değerler hiçbir hedef tablo/log/staging export’una taşınmamalıdır.
11. **Duplicate active state:** Aynı mold için çoklu `STARTED`, completed package içinde duplicate serial, çelişkili mechanism suitable flags ve NCR evaluator mismatch hard constraint öncesi conflict queue gerektirir.
12. **INO şema evrimi:** `MainForm.BuildTableSchema` yüklenen header’lardan DataTable kurar ve computed/internal kolon ekler; farklı dönem CSV header’ları aynı şema kabul edilmemelidir.
13. **SQL kopyası:** SQL Server importu bulk replacement/snapshot olabilir; CSV ile SQL satırları iki bağımsız gerçek kaynak gibi double-import edilmemelidir.
14. **Encoding/mojibake:** Bazı kaynak UI metinlerinde bozuk Türkçe karakter örnekleri görülür. CSV encoding detection ve raw byte hash olmadan otomatik düzeltme veri değiştirebilir.

Her import run idempotent olmalı; source byte hash, row number/raw JSON, derived business key, target UUID map, warning/reject ve reconciliation sonucu saklanmalıdır. Production tablolarına doğrudan CSV okuyan migration yazılmamalıdır.

## 7. Güvenlik açısından dikkat edilmesi gereken noktalar

- **Authentication:** Legacy PBKDF2 hash/salt, eski plain/protected alanlar ve INO credentials migrate edilmemeli. OIDC/AD subject ile legacy username snapshot eşlemesi ayrı olmalı; kullanıcı adı tek başına güven sınırı olmamalıdır.
- **Authorization:** Template/button gizlemek yeterli değildir. `AppState.Can*`, `ApplyAccess`, `ApplyPermissions` ve INO `RowEditForm.ApplyReadOnlyStyle` davranışları permission + object/field scope testlerine çevrilmelidir. Admin/“Yönetici” eş anlamlı sayılmamalıdır.
- **Dosya erişimi:** Storage key kullanıcıdan alınan path olmamalı; signed/authorized download endpoint kullanılmalı. MIME sniffing, allowlist, size limit, antivirus/quarantine, filename sanitization, range request auth ve `Content-Disposition` uygulanmalıdır.
- **Legacy decrypt:** Anahtarlar sadece kontrollü offline migration ortamında bulunmalı; plaintext geçici dosya diskte/logda kalmamalı; hash doğrulaması sonrası güvenli cleanup yapılmalıdır. V1 decrypt başarısızlığı sessiz skip değildir.
- **CAD worker:** DWG/AutoCAD işleme untrusted input çalıştırır; Django web processinden ayrı Windows worker, düşük yetki, izole queue, timeout/CPU/disk limit ve çıktı doğrulaması gerekir.
- **Injection/XSS:** CSV açıklamaları, email HTML, dashboard HTML ve CAD metadata untrusted kabul edilmelidir. Django autoescape kapatılmamalı; rich text gerekiyorsa sanitizer; spreadsheet exportta formula injection önlemi kullanılmalıdır.
- **CSRF/session:** Tüm mutationlar POST ve CSRF korumalı; OIDC callback/state/nonce doğrulamalı; secure/HttpOnly/SameSite cookie, session rotation, idle/absolute timeout ve forced logout politikası tanımlanmalıdır.
- **Concurrency/IDOR:** UUID tahmin edilemezlik authorization değildir. Her entity lookup’ta scope kontrolü; critical transition’da lock + idempotency key + DB invariant gerekir.
- **Secrets/PII:** SMTP/OIDC/storage/DB secret’ları env/secret store’da; connection string ve decrypt key loglanmaz. Audit before/after içinde password, token, attachment content ve gereksiz kişisel veri redakte edilir.
- **Audit:** Domain audit append-only, uygulama rolünden UPDATE/DELETE kapalı ve mümkünse ayrı DB role/partition olmalı. Actor, request correlation, IP, user-agent ve reason kaydı saat senkronizasyonuyla tutulmalıdır.
- **Supply/deploy:** Pinned/locked dependencies, image scanning, non-root container, TLS, PostgreSQL/Redis’in private networkte tutulması, backup encryption ve restore tatbikatı WP-001 platform kararlarına girmelidir.

## 8. Django mimarisi açısından öneriler

1. Master Plan app sınırlarını başlangıç rehberi kabul edin; çok küçük app’lerle FK döngüsü üretmek yerine aggregate ownership’i açık yazın. `core` iş kuralı çöplüğü olmamalıdır.
2. State-changing use case’leri typed command/service fonksiyonlarında; read-heavy dashboardları selector/query service/read model’de tutun. Form/view/model `save()` içine transition dağıtmayın.
3. Transition servisleri `transaction.atomic`, aggregate row lock, current-state assertion, permission, domain mutation, audit/outbox işlemini tek sınırda yürütmelidir. Email/Celery publish transaction içinde gönderilmemeli; transactional outbox + `on_commit` wake-up kullanılmalıdır.
4. Custom user model WP-001’in ilk migration’ından önce seçilmelidir. OIDC identity (`issuer`, `subject`) ve historical legacy actor birbirine karıştırılmamalıdır.
5. Decimal, timezone ve normalization yardımcılarını merkezi ve saf fonksiyonlar yapın; VB kaynaklı golden fixtures ile parity test edin. Server sonucu client’tan kabul etmesin.
6. Permission modeli Django permission + explicit scope policy şeklinde olmalı. Her service hem action permission hem record/department/drawing scope kontrol etmelidir; test matrisi `YETKI_MATRISI`nden üretilmelidir.
7. Historical aggregate’lerde snapshot + nullable/protected FK yaklaşımını tutarlı uygulayın. Master rename geçmiş raporu değiştirmemelidir.
8. Legacy migration ayrı `legacy_migration` app/management commands altında staging-transform-load-reconcile fazlarına bölünsün; runtime modelleri legacy parse koduna bağımlı olmasın.
9. File storage abstraction ilk günden kullanılsın; DB’ye binary koymayın. Upload finalize ancak scan/hash doğrulamasından sonra olsun; orphan cleanup referans ve retention aware çalışsın.
10. PostgreSQL constraintleri invariantların son savunmasıdır; pending DECISION maddeleri feature/config ile belirsiz bırakılmamalı, ADR sonucu olmadan hard-coded edilmemelidir.
11. Test piramidi: saf domain parity unit testleri; transaction/concurrency integration testleri; permission matrix parameterized testleri; migration golden/reject/idempotency testleri; kritik HTMX akışları için Playwright.
12. Observability: structured log/correlation ID, transition ve outbox metrics, Celery retry/dead-letter görünürlüğü, Sentry-compatible exception tracking, DB slow query takibi ve immutable security eventleri.

Önerilen bağımlılık yönü: identity/core masters → products/drawings/control points → inspections; molding/commissioning/lab/shift kendi aggregate’lerine sahip olur; tickets/notifications/audit diğer app modellerini import ederek döngü kurmak yerine açık orchestration/service ve gerektiğinde string/UUID event reference kullanır. Django signal’ları kritik business workflow için kullanılmamalıdır.

## 9. Açık mimari kararlar (ADR candidates)

| ADR | Karar | WP-001 etkisi |
|---|---|---|
| ADR-001 | Drawing identity ve TR/scope normalization | Model constraint/migration key |
| ADR-002 | Aynı mold/token/machine için aktif binding cardinality | Partial unique + UI davranışı |
| ADR-003 | Makine değişim açıklaması zorunluluğu | Validation/acceptance test |
| ADR-004 | Commissioning `ŞARTLI ONAY` state/terminal anlamı | Enum/state machine |
| ADR-005 | SPC historical correction fiziksel snapshot update mı analytical override mı | Immutability/reporting |
| ADR-006 | Completed package/measurement/test doğrudan Admin edit mi correction/reopen mı | Revision tabloları/audit |
| ADR-007 | Identity provider: Authentik/OIDC mi doğrudan LDAP mı; dev/service account akışı | Custom user/settings/deploy |
| ADR-008 | Authorization scope modeli ve “Yönetici” read-only semantiği | Permission schema/API contract |
| ADR-009 | File storage başlangıcı, encryption-at-rest, malware scan ve legacy decrypt runbook | `core_file_object`/infra |
| ADR-010 | DWG: ayrı Windows CAD worker, dış servis veya v1 kapsam dışı | Queue/deployment topology |
| ADR-011 | Timezone, DST ve legacy naive timestamp yorumlama | Global settings/import correctness |
| ADR-012 | Product–mold ilişkisinin zaman/version semantiği | ERD constraintleri |
| ADR-013 | Domain transition history/outbox şeması | İlk ortak altyapı migrationı |
| ADR-014 | Data retention, legal hold, soft-delete/cancel ve purge | FK/delete/partition |
| ADR-015 | INO hedef domain şeması ve ikinci identity store’un kapatılması | App boundary/migration |
| ADR-016 | Cutover source-of-truth: CSV freeze/snapshot, olası SQL kopyası ve incremental delta yöntemi | Migration architecture |
| ADR-017 | Deployment: Compose sınırı, HA/backup/RPO/RTO, Redis/Celery zorunluluğu | WP-001 skeleton |
| ADR-018 | Numeric precision/rounding/unit policy | DecimalField/check/result parity |

## 10. WP-001 başlamadan önce çözülmesi gereken blocker'lar

### Hard blocker

1. **WP-001 kapsam/çıktı çelişkisi:** Master Plan WP-001 platform skeleton talep ederken ERD’nin “ilk migration seti” accounts/core/products/drawings/control points gibi sonraki domainleri de sayar. WP-001’in model/migration üretip üretmeyeceği ve exact acceptance criteria yazılı olarak kilitlenmelidir.
2. **Kimlik kararı:** Custom user model ve OIDC/LDAP sağlayıcısı ilk migration’dan sonra maliyetli değişir. ADR-007 ve legacy actor eşleme yaklaşımı onaylanmalıdır.
3. **Canlı veri discovery paketi:** Authoritative CSV/binary snapshot, header manifesti, encoding, dosya sayıları/hashleri ve (varsa) SQL Server kopyasının statüsü erişilebilir değildir. En az anonimleştirilmiş temsilî fixture + profiler çıktısı sağlanmadan domain constraint/migration tasarımına başlanmamalıdır.
4. **Drawing identity profiling:** `(normalized TR, scope)` ve product grouping varsayımları doğrulanmadan unique constraint yazılamaz.
5. **AS-IS/TO-BE karar sahipliği:** DOMAIN_RULES açık ürün kararları için iş sahibi ve onay mekanizması belirtilmeli; en az binding, machine reason, package correction, commissioning conditional approval kararları kod sırasına göre sonuçlandırılmalıdır.
6. **Yetki baseline:** `YETKI_MATRISI`, `AppState/AuthorizationService`, form-level ve INO field-level davranışlarındaki farkların onaylı tek permission/scope matrisi üretilmelidir.
7. **Timezone ve numeric policy:** Legacy naive timestamp timezone’u ve decimal precision/rounding kabul kriteri belirlenmelidir; aksi halde daha ilk settings/model seçimleri historical sonucu değiştirebilir.
8. **Secrets/files cutover feasibility:** Legacy drawing decrypt key erişimi, V1/V2 sayıları, decrypt test sonucu, hedef storage ve büyük/zararlı dosya politikası doğrulanmalıdır.

### WP-001'i durdurmayan, fakat ilgili domain work package'ını durduran kapılar

- DWG worker seçimi engineering/CAD work package’ına kadar ertelenebilir.
- SPC correction semantiği SPC work package’ına kadar; commissioning conditional state commissioning work package’ına kadar ADR ile pending tutulabilir.
- INO tam typed şeması INO profiling work package’ına kadar ertelenebilir; ancak authentication’ın birleşeceği WP-001’de kararlaştırılmalıdır.
- RPO/RTO ve production HA ayrıntıları local development skeleton’ı engellemeyebilir; fakat production settings/deploy işi öncesi zorunludur.

## Son değerlendirme

Doküman seti hedef yönü güçlü biçimde tanımlar ve AS-IS/TO-BE ayrımındaki önemli hataları düzeltir. Bununla birlikte WP-001’in sınırı, ilk identity modeli, authoritative canlı veri profili, drawing key, permission scope, timezone/numeric politika ve legacy file/decrypt uygulanabilirliği henüz kanıtlanmış/onaylanmış değildir. İlk migration kararlarını geri dönülmez biçimde etkileyecek bu maddeler çözülmeden “hazır” demek güvenli değildir.

**READY_FOR_WP001 = NO**
