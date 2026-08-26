# Teknik Resim Ölçüm Kontrol — Yetki Matrisi

Güncelleme tarihi: **24.07.2026**

Bu belge, uygulamadaki güncel `AppState` yetkileri ve formlardaki işlem kontrolleri esas alınarak hazırlanmıştır.

Programdaki **Yetki Matrisi** penceresi bu ana yetki başlıklarını, giriş ve yardımcı pencereler dahil **63 ayrı ekran** olarak açar. Penceredeki **Tüm Ekranlar CSV** düğmesi, ekranda görülen ayrıntılı matrisi dışarı aktarır.

Not: Eski kayıtlarda görülebilen **Kalite Kontrol Kullanıcısı** rol adı artık program içinde **Plastikhane Kalite Kontrol** olarak değerlendirilir ve yeni kullanıcı seçimlerinde ayrı rol olarak gösterilmez.

## Gösterim

- **T** — Tam yetki: ekranın standart kayıt/işlem özelliklerini kullanabilir.
- **S** — Sınırlı yetki: hücrede veya aşağıdaki notlarda belirtilen kapsamda işlem yapabilir.
- **O** — Salt okunur: görüntüleyebilir, değişiklik yapamaz.
- **—** — Erişim yok; ana menüde gösterilmez ve form/servis katmanında engellenir.

## Ana Yetki Matrisi

| Rol | Ölçüm | Kalite Ticket | Plastikhane Vardiya | Mekanizma Kontrol | İNO-1 / İNO-2 | Kalıp Bağlama | Bağlanacak Kalıp | Teknik Resim Arama | Bağlama Dashboard | Üretim Ticket | Kalıp Ticket | Ürün / Teknik Resim | Kontrol Ölçüleri | SPC Dashboard | MSA Dashboard | Test Talepleri | Paket Sayaç Kontrolleri | Kullanıcı / Sistem |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| Üretim Kullanıcısı | — | — | O | — | — | S | S | — | S | O | O | — | — | — | — | — | — | — |
| Üretim Etiket | — | — | — | — | — | — | O | O | — | — | — | — | — | — | — | — | — | — |
| Üretim Yöneticisi | — | O | O | — | — | T | T | O | T | T | T | — | — | — | — | — | — | — |
| Kalite Kontrol Yöneticisi | T | T | S | T | S | — | O | O | — | T | T | — | — | O | O | T | S | — |
| Giriş Kalite Kontrol | S | — | — | — | — | — | — | — | — | — | — | — | — | — | — | S | — | — |
| Mekanizma Kalite Kontrol | — | — | O | S | T | — | O | — | — | — | — | — | — | — | — | S | — | — |
| Mekanizma Yöneticisi | — | — | O | O | T | — | O | — | — | — | — | — | — | — | — | — | — | — |
| Plastikhane Kalite Kontrol | T | T | S | T | — | — | O | O | — | — | T | — | — | — | — | S | — | — |
| Kalite Laboratuvar | — | — | — | — | S | — | — | — | — | — | — | — | — | — | — | S | S | — |
| Teknik Resim | — | — | — | — | — | — | — | — | — | — | — | S | T | — | — | — | — | — |
| Planlama | — | — | — | — | O | — | — | — | — | — | — | — | — | — | — | — | — | — |
| Yönetici | S | O | O | O | O | — | O | — | O | O | O | O | O | O | O | — | O | — |
| Admin | T | T | T | T | T | T | T | T | T | T | T | T | T | T | T | T | T | T |

## Sınırlı Yetkilerin Açıklaması

| Rol / Ekran | Gerçek kapsam |
|---|---|
| Üretim Kullanıcısı — Kalıp Bağlama | Bağlama başlatabilir ve tamamlayabilir; aktif kayıtlarda yalnızca kendi başlattıklarını görür. |
| Üretim Kullanıcısı — Bağlanacak Kalıp | Bağlama işlemlerini kullanabilir; Excel'den plan aktaramaz. |
| Üretim Kullanıcısı — Dashboard | Yalnızca kendi bağlama kayıtlarını ve özetini görür. |
| Üretim Etiket — Bağlanacak Kalıp | Bağlanacak kalıp listesini salt okunur görür. |
| Teknik Resim Arama yetkisi olan roller | Üretim Etiket, Üretim Yöneticisi, Plastikhane Kalite Kontrol, Kalite Kontrol Yöneticisi ve Admin TR kodu ile yalnızca aktif **Plastik Resmi** teknik resimlerini arar ve program içi görüntüleyicide salt okunur açar. Dosya yolu veya CSV konumu gösterilmez. |
| Üretim Yöneticisi — Kalite Ticket | Ticket ve ölçüm sonuçlarını görüntüler; görüldü yapamaz, kontrol girişi açamaz ve ticket kapatamaz. |
| Kalite Kontrol Yöneticisi — İNO | Onay rolüyle izin verilen alanları düzenleyebilir; tam satır ekleme/silme yetkisi yoktur. |
| Giriş Kalite Kontrol — Ölçüm | Yalnızca `Giriş Kalite Kontrol Resmi` tipindeki teknik resimler için ölçüm girişi ve geçmişi kullanabilir; `Plastik Resmi` kayıtlarına erişemez. |
| Kalite Kontrol Yöneticisi — Plastikhane Vardiya | Kayıtları görüntüleyebilir ve silebilir; yeni kayıt ekleyemez veya düzenleyemez. |
| Plastikhane Kalite Kontrol — Plastikhane Vardiya | Yeni kayıt ekleyebilir ve düzenleyebilir; kayıt silemez. Kalıp Tadilat seçildiğinde vardiya kaydına bağlı Kalıp Ticketı oluşturabilir. |
| Plastikhane Kalite Kontrol — Kalıp Ticket | Kalıp Ticket işlemi yapabilir; ayrıca Plastikhane Vardiya kaydındaki Kalıp Tadilat seçeneği üzerinden yeni ticket oluşturabilir. |
| Mekanizma Kalite Kontrol — Mekanizma Kontrol | Bekleyen teslimleri kontrol edip uygun/uygun değil sonucu verebilir; yeni teslim oluşturamaz. |
| Mekanizma Yöneticisi — Mekanizma Kontrol | Kayıt listesini ve satır ayrıntılarını salt okunur görüntüler; yeni teslim oluşturamaz veya bekleyen teslimi sonuçlandıramaz. |
| Kalite Laboratuvar — İNO | Onay rolüyle izin verilen alanları düzenleyebilir; tam satır ekleme/silme yetkisi yoktur. |
| Talep oluşturan roller — Test Talepleri | Giriş Kalite Kontrol, Mekanizma Kalite Kontrol, Plastikhane Kalite Kontrol, Kalite Kontrol Yöneticisi ve Admin yeni talep oluşturabilir; laboratuvar sonucu giremez. Kendi açık talebini iptal edebilir. |
| Sonuçlandıran roller — Test Talepleri | Kalite Kontrol Yöneticisi, Kalite Laboratuvar ve Admin talepleri işleme alabilir ve atanmış testleri tanımlı sırayla tamamlayabilir. Her testte işlemi yapan kullanıcı ile tarih/saat otomatik kaydedilir. Tüm test adımları çözülmeden genel sonuç ve laboratuvar raporu girilemez. |
| Kalite Kontrol Yöneticisi / Admin — Test Akışı | Talebe test atayabilir; uygulama başlamadan önce test sırasını değiştirebilir. Zorunlu gerekçeyle sıradaki testi atlayabilir veya sıra bütünlüğünü koruyarak son çözülen adımdan geriye doğru test açabilir. |
| Kalite Laboratuvar / Kalite Kontrol Yöneticisi — Paket Sayaç Kontrolleri | Yeni kontrol oluşturabilir, taslak kaydı düzenleyebilir ve tamamlayabilir. Tamamlanan kayıtlar geçmiş bütünlüğü için salt okunur olur; kalıcı silme yalnızca Admin rolündedir. |
| Giriş / Mekanizma / Plastikhane Kalite Kontrol — Paket Sayaç Kontrolleri | Erişim yoktur; ana menüde buton gösterilmez ve form/servis katmanında doğrudan erişim engellenir. |
| Yönetici — Paket Sayaç Kontrolleri | Kontrol kayıtlarını ve seri numarası detaylarını salt okunur görüntüleyebilir; yeni kayıt oluşturamaz, düzenleyemez, tamamlayamaz veya silemez. |
| Teknik Resim — Ürün / Teknik Resim | Ürün ve teknik resim kaydı ekleyip düzenleyebilir; kayıt silme yalnızca Admin'dedir. |
| Yönetici — Ölçüm | Yalnızca Ölçüm Geçmişi ve kayıt inceleme pencerelerini salt okunur kullanır. |
| Yönetici — Ticketlar | Kalite, Üretim ve Kalıp Ticketlarını görüntüler; görüldü yapamaz, oluşturamaz veya kapatamaz. |
| Yönetici — Bağlanacak Kalıp | Listeyi görüntüler; bağlama başlatamaz ve Excel'den plan aktaramaz. |
| Yönetici — Teknik Resim | Ürün/teknik resim, eksik resim ve kontrol ölçülerini görüntüler; ekleme, düzenleme veya silme yapamaz. |
| Yönetici — Sistem | Kullanıcı Yönetimi, Log Kayıtları ve Program Güncelleme ekranlarına erişemez. Yetki Matrisi ayrı bir görüntüleme yetkisi olarak korunur. |
| SPC Dashboard | Admin tam yetkilidir; riskli ölçüler, analiz, geçmiş limit düzeltme ve rapor işlemlerini kullanabilir. Kalite Kontrol Yöneticisi ve Yönetici ekranı salt okunur görüntüler; SPC geçmiş limit düzeltme ve müdahale işlemleri yapamaz. |
| MSA Dashboard | Ölçüm cihazı tanımları ve kalibrasyon/geçerlilik bilgileri izlenir. Admin cihaz ekleyebilir, düzenleyebilir ve silebilir; Kalite Kontrol Yöneticisi ve Yönetici salt okunur görüntüler. |

## Ekran Bazlı Önemli Ayrıntılar

### Teknik Resim Arama

- **Üretim Etiket, Üretim Yöneticisi, Plastikhane Kalite Kontrol, Kalite Kontrol Yöneticisi** ve **Admin** bu ekranı açabilir.
- Ekran sadece aktif `Plastik Resmi` teknik resimlerini listeler.
- Kullanıcı TR kodu yazarak filtreleme yapar; satıra çift tıklayarak veya **Teknik Resmi Aç** butonuyla resmi görüntüler.
- Teknik resim program içi görüntüleyicide açılır; harici açma ve dosya konumu gösterilmez.

### Ölçüm Girişi ve Ölçüm Geçmişi

- **Plastikhane Kalite Kontrol, Kalite Kontrol Yöneticisi, Giriş Kalite Kontrol ve Admin** ölçüm girişi yapabilir.
- **Giriş Kalite Kontrol** ölçüm ekranında yalnızca `Giriş Kalite Kontrol Resmi` deposundaki teknik resimleri ve bu kapsamdaki ölçüm geçmişini görür.
- **Plastikhane Kalite Kontrol** ölçüm ekranında yalnızca `Plastik Resmi` deposundaki teknik resimleri ve bu kapsamdaki ölçüm geçmişini görür.
- **Yönetici** yalnızca Ölçüm Geçmişi ve kayıt inceleme ekranlarını salt okunur kullanır.
- Ölçüm geçmişindeki tüm kayıt grubunu silme yetkisi yalnızca **Admin** rolündedir.
- CSV dosya konumları ve CSV klasörü düğmeleri güvenlik amacıyla **hiçbir role gösterilmez**.

### Ürün / Teknik Resim ve Kontrol Ölçüleri

- **Teknik Resim** rolü teknik resim ekleyebilir ve düzenleyebilir; kayıt silemez.
- **Admin** teknik resim yönetimi ve silme dahil tüm işlemleri yapabilir.
- **Yönetici** teknik resim ve kontrol ölçülerini salt okunur görür.
- **Üretim Etiket, Üretim Yöneticisi, Plastikhane Kalite Kontrol** ve **Kalite Kontrol Yöneticisi** bu yönetim ekranlarına bu yetki üzerinden erişmez; yalnızca ayrı **Teknik Resim Arama** ekranından aktif plastik resimlerini salt okunur görüntüler.
