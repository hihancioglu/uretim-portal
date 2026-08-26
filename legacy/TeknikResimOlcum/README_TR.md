# Teknik Resim Ölçüm Kontrol Programı

VB.NET WinForms masaüstü kalite kontrol ölçüm kayıt programıdır.

## İlk Giriş

- Kullanıcı adı: `admin`
- İlk kurulumda `admin` hesabı için rastgele ve tek sefer gösterilen geçici parola üretilir.
- İlk/geçici parolayla giriş yapan kullanıcı yeni parola belirlemeden programa devam edemez.
- Tüm kullanıcılar ana ekrandaki `Şifremi Değiştir` seçeneğiyle kendi parolalarını değiştirebilir.

İlk çalışmada program şu klasörleri oluşturur:

- `Data`
- `Drawings`
- `Temp`

## Ana Özellikler

- Admin / kullanıcı girişi
- PDF teknik resmi kurulum başına rastgele anahtar ve AES-GCM ile `.pdf.enc` olarak şifreli saklama
- PDF teknik resmi program içinde görüntüleme
- Admin ekranında PDF açıkken kontrol ölçüsü tanımlama
- Ölçü noktası için PDF üzerinde yaklaşık X/Y konumu alma
- Her TR ve revizyon için farklı sayıda kontrol ölçüsü
- Ölçüm girişi
- Otomatik OK / NOK hesabı
- Ölçüm kayıtlarını CSV'ye yazma
- Log kayıtlarını CSV'ye yazma

## PDF İçinden Ölçü Tanımlama

1. Admin olarak giriş yapın.
2. `Ürün / TR Yönetimi` ekranından TR, revizyon ve PDF teknik resmi yükleyin.
3. `Kontrol Ölçüleri` ekranını açın.
4. Sol tarafta PDF program içinde açılır.
5. `PDF'ye tıklayınca X/Y konumu al` seçeneğini işaretleyin.
6. PDF üzerinde ölçünün bulunduğu yere tıklayın.
7. Sağ taraftaki `X %` ve `Y %` alanları otomatik dolar.
8. Ölçü No, Ölçü Adı, Nominal, Alt Tol., Üst Tol. ve diğer alanları doldurup kaydedin.

> Not: X/Y bilgisi görünür PDF alanına göre yaklaşık yüzdedir. İlk sürümde ölçü balonu çizmez; ölçünün PDF üzerindeki konumunu kayıt altına alır.

## CSV Dosyaları

- `Data/Users.csv`
- `Data/Products.csv`
- `Data/ControlPoints.csv`
- `Data/MeasurementRecords.csv`
- `Data/AuditLog.csv`

## Derleme

Visual Studio 2022 ile `TeknikResimOlcum.vbproj` dosyasını açıp çalıştırabilirsiniz.

Komut satırıyla EXE almak için:

```bat
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true
```

veya:

```bat
build_release.bat
```

## Gereksinim

PDF'nin program içinde görüntülenmesi için Microsoft Edge WebView2 Runtime gerekir. Windows 10/11 sistemlerin çoğunda hazır gelir. Yoksa Microsoft Edge WebView2 Runtime kurulmalıdır.

## FIX4 Notları

- Kontrol Ölçüleri ekranında sağ tarafta tanımlı ölçüler artık ayrı bir liste olarak gösterilir.
- Ölçü No alanı elle yazılmaz; seçili TR ve revizyona göre otomatik üretilir.
  - Örnek: `TR2090_01_M001`, `TR2090_01_M002`
- Yeni ölçü kaydedildikten sonra program otomatik olarak sonraki ölçü numarasını hazırlar.
- Sağ taraftaki gri alan düzenlendi; liste paneli beyaz arka planlı ve kolon başlıklı hale getirildi.

## FIX5 Notu
Ürün / Teknik Resim Yönetimi ekranına kayıtlı teknik resim listesi eklendi. Kaydet / Güncelle işleminden sonra kayıt otomatik olarak listede gösterilir ve seçilir. Liste satırına tek tıklayınca bilgiler forma alınır, çift tıklayınca teknik resim açılır.


## FIX13 Notu

PDF görüntüleme `FitH` yerine `Fit / page-fit` olacak şekilde değiştirildi. Böylece dikey veya yatay PDF sayfası ilk açılışta görüntüleme alanına komple sığdırılır. Yakınlaştırma sonrası `Fit Sayfa` butonu görünümü tekrar tam sayfa sığdırma moduna alır.


## FIX15 Notu

PDF görüntüleme tekrar düzenlendi:
- Dikey/yatay PDF oranı `/CropBox` veya `/MediaBox` üzerinden okunur.
- `/Rotate 90` ve `/Rotate 270` bulunan PDF'lerde oran ters çevrilir.
- `Fit Sayfa` artık görüntüleme alanının hem genişliğine hem yüksekliğine göre hesaplanır.
- Yakınlaştırma butonları artık görünümü otomatik aşağı kaydırmaz; mevcut konumu korur.
- PDF alanında açıklama/legend yoktur; sadece teknik resim, ölçü balonları ve giriş kutucuğu vardır.


## FIX16 Notu

PDF görüntüleme yaklaşımı değiştirildi:
- WebView2 içindeki yerleşik PDF eklentisi artık ölçüm/tanımlama ekranlarında doğrudan kullanılmaz.
- Şifreli PDF geçici olarak çözülür, ilk sayfası PNG görüntüye render edilir.
- Ekranda PDF yerine bu görüntü gösterilir; ölçü balonları ve kutucuklar aynı koordinat sisteminde çalışır.
- Bu değişiklik nested scrollbar, yakınlaştırınca siyah alan/boş alan görünmesi ve ölçü balonlarının kayması sorunlarını çözmek içindir.
- Projeye `Docnet.Core` NuGet paketi eklendi.


## FIX17 Notu

PDF görüntüleme alanında `Fit Sayfa` davranışı değiştirildi:
- Görüntü artık PDF panelinin tamamını doldurur.
- Üstten/alttan veya sağdan/soldan boşluk bırakmaz.
- Ölçü balonları ve giriş kutucuğu aynı koordinat sistemi içinde kaldığı için zoom sırasında beraber hareket eder.
- Bu mod, teknik resmin görüntüleme alanına tam oturması için sayfayı panel oranına göre esnetir.

Pencereler artık büyük ekran / maksimize açılır:
- Ana menü
- Ürün / Teknik Resim Yönetimi
- Kontrol Ölçüleri
- Ölçüm Girişi
- Ölçüm Geçmişi
- Kullanıcı Yönetimi
- Log Kayıtları


## FIX18 Notu

`Fit Sayfa` davranışı tekrar düzenlendi:
- Teknik resim artık görüntü alanına sığarken oranı bozulmaz.
- En-boy oranı korunur.
- PDF/teknik resim tamamen görünür.
- Görüntü alanı ile teknik resmin oranı farklıysa sağ/sol veya üst/alt tarafta boşluk kalması normaldir; bu oran bozulmadan fit davranışının gereğidir.
- Ölçü balonları ve giriş kutucuğu aynı koordinat sisteminde kalır.


## FIX19 Notu

Fit / zoom sistemi tekrar düzeltildi:
- Fit Sayfa artık görüntünün doğal PNG boyutundan oran hesaplar.
- `100%` = teknik resmin tamamı görünür şekilde fit edilmiş hâlidir.
- `80%` = fit edilmiş görüntünün %80 ölçeğidir.
- `+` ve `-` mevcut konumu koruyarak çalışır.
- En-boy oranı korunur; görüntü esnetilmez.
- Teknik resim ile ölçü balonları aynı `stage` üzerinde kaldığı için koordinat kayması olmamalıdır.


## FIX20 Notu

PDF görüntü kalitesi ve render yönü iyileştirildi:
- PDF ilk sayfası artık daha yüksek çözünürlükte PNG'ye çevrilir (`maxSide=4200`).
- Sistem birden fazla render oranını dener:
  - PDF'den okunan oran
  - ters oran
  - yatay A4
  - dikey A4
- Kenar kırpılma/taşma ihtimaline göre en uygun görüntüyü otomatik seçer.
- Bu düzeltme, önceki sürümlerde görülen sağdan/üstten kesinti ve düşük kalite sorunlarını azaltmak içindir.


## FIX21 Notu

Fit davranışı tekrar ayarlandı:
- Önceki sürümde `100% Fit` görüntüsünde bazı PDF'lerde üst kısım kesilebiliyordu.
- Gözlemde `90%` görünüm tam sayfayı gösterdiği için `Fit Sayfa` artık güvenli fit payı ile çalışır.
- Ekrandaki `100%`, teknik resmin tamamının görünmesi için güvenli tam-fit görünümüdür.
- `90%`, bu güvenli fit görünümünün %90 ölçeğidir.
- Render kalitesi için PNG oluşturma çözünürlüğü 4200'den 5200'e çıkarıldı.


## FIX22 Notu

PDF fit ve kalite ayarı yeniden güncellendi:
- `Fit Sayfa` artık daha güvenli payla hesaplanır; sayfanın üst başlığı dahil tamamının görünmesi hedeflenir.
- İlk yüklemede WebView2 alanının geç yerleşmesi ihtimaline karşı fit işlemi birkaç kez otomatik tekrar uygulanır.
- `100%`, güvenli tam sayfa görünümüdür.
- `+ / -` ile zoom yapıldığında 100% ve altına düşüşte scroll konumu başa alınır.
- Render kalitesi için PNG üretim çözünürlüğü 6000 seviyesine çıkarıldı.


## FIX23 Notu

Tolerans hesaplama düzeltildi:
- Alt Tolerans artık kullanıcı `1` yazsa bile `-1` kabul edilir.
- Örnek: Nominal `1`, Alt Tol. `1`, Üst Tol. `1` ise limitler `0` ve `2` olur.
- Girilen değer `1` bu durumda `OK` olur.
- Eski kaydedilmiş kontrol ölçülerinde Alt Tol. pozitif yazılmışsa, program okurken bunu otomatik düzeltir.


## FIX24 Notu

Bu sürümde:
- Teknik resim alanına yukarı / aşağı / sol / sağ kaydırma butonları eklendi.
- Sağ taraftaki ölçü tanımlama alanı sadeleştirildi.
- Tolerans mantığı korunur:
  - Nominal = 1
  - Alt Tolerans = 1
  - Üst Tolerans = 1
  - Kabul aralığı = 0 ile 2 arasıdır.
- Alt tolerans pozitif yazılsa bile program bunu nominalden düşerek hesaplar.


## FIX25 Notu

Kontrol Ölçüleri ekranında PDF kontrol çubuğu sol teknik resim alanından sağ panele taşındı:
- PDF Göster
- Fit Sayfa
- - / +
- ↑ ↓ ← →
- PDF'ye tıklayınca X/Y konumu al
- Konumu Temizle

Sol teknik resim alanında artık yalnızca teknik resim görüntüsü bulunur.


## FIX26 Notu

Kontrol Ölçüleri ekranı tekrar gözden geçirildi:
- Sağ üst PDF kontrol bölümü FlowLayoutPanel yapısına alındı; butonlar sağ tarafta kesilmez.
- `Konumu Temizle` butonu artık taşma yapmadan görünür.
- PDF kontrol bölümü iki satıra düzgün sarılabilir.
- Sol tarafta sadece teknik resim kalır.
- Sağ panel biraz daha dengeli genişlik alacak şekilde splitter oranı güncellendi.


## FIX27 Notu

Aynı PDF layoutu Ölçüm Girişi ekranına da uygulandı:
- Sol taraf: yalnızca teknik resim görüntüsü.
- Sağ taraf: PDF kontrol çubuğu, ölçüm bilgileri ve ölçüm listesi.
- Kontrol Ölçüleri ve Ölçüm Girişi ekranları artık aynı düzen mantığıyla çalışır.


## FIX28 Notu

PDF kullanılan ekranlar tekrar gözden geçirildi:
- Teknik resim sol tarafta daha büyük ve fit görünecek şekilde ayarlandı.
- Sol/sağ oranı teknik resme biraz daha fazla alan verecek şekilde güncellendi.
- Sağ panelde PDF kontrol çubuğu sadeleştirildi.
- Kontrol Ölçüleri ekranında ölçü tanımlama alanı daha kompakt hizalandı.
- Ölçüm Girişi ekranı da aynı layout mantığına göre güncellendi.


## FIX29 Notu

Kontrol Ölçüleri ekranındaki buton satırı düzeltildi:
- `Kaydet / Güncelle`
- `Seçili Ölçüyü Pasif Yap`
- `Yeni Ölçü`

Bu butonlar artık alt liste başlığına sıkışmayacak ve tam yükseklikte görünecek şekilde form paneli yüksekliği ve buton konumları düzenlendi.


## FIX30 Notu

TR seçim alanlarına filtre eklendi:
- Kontrol Ölçüleri ekranı
- Ölçüm Girişi ekranı

`TR Filtre` alanına TR kodu, revizyon veya ürün adından bir parça yazılabilir.
Örnekler:
- `TR209`
- `209 01`
- `TR2090 01`
- ürün adından bir kelime

Filtre boş bırakılırsa tüm aktif teknik resimler listelenir.


## FIX31 Notu

Ölçüm Girişi ekranındaki buton satırı düzeltildi:
- `Ölçüm Kaydet`
- `Temizle`

Butonlar artık alt ölçüm listesi başlığına sıkışmayacak şekilde panel yüksekliği, buton yüksekliği ve konumları düzenlendi.


## FIX32 Notu

Ürün / Teknik Resim Yönetimi ekranına kayıtlı teknik resimler için filtre eklendi:
- Liste Filtresi: TR kodu, revizyon, ürün adı veya şifreli dosya adına göre arar.
- Durum filtresi: TÜMÜ / AKTİF / PASİF.
- Filtreyi Temizle butonu eklendi.
- Kayıt sayısı artık filtrelenen / toplam adet şeklinde gösterilir.

Ana sayfa butonları yeniden düzenlendi:
- Daha okunabilir kart görünümü.
- Her butona kısa açıklama eklendi.
- Ana ekran yerleşimi daha düzenli hale getirildi.


## FIX33 Notu

Ana sayfa buton yazıları sadeleştirildi:
- Butonlarda artık yalnızca buton adı görünür.
- Açıklama satırları kaldırıldı.
- Buton yüksekliği buna göre azaltılıp daha temiz görünüm sağlandı.


## FIX34 Notu

Giriş ekranındaki sabit varsayılan şifre kaldırıldı:
- İlk Admin parolası rastgele oluşturulur ve yalnızca ilk kurulumda gösterilir.
- İlk girişte parola değişikliği zorunludur.
- Sonraki açılışlarda giriş ekranında parola bilgisi gösterilmez.


## FIX35 Notu

Ölçüm Geçmişi ekranı yeniden düzenlendi:
- Genel arama filtresi eklendi.
- TR Kodu, İş Emri No, Seri No, Ölçü No/Adı ve Sonuç filtreleri eklendi.
- Temizle butonu eklendi.
- Kayıt sayısı filtrelenen / toplam satır şeklinde gösterilir.
- Grid kolonları sabit ve okunabilir genişliklere alındı.
- Yatay kaydırma desteklenir.
- OK / NOK / HATALI satırları renklendirilir.


## FIX36 Notu

Kullanıcı Yönetimi ekranı düzeltildi:
- Kullanıcı listesi artık altta beyaz DataGridView olarak görünür.
- Liste filtresi ve durum filtresi eklendi.
- ADMIN ekranında şifre kolonu eklendi.
- Yeni oluşturulan veya şifresi güncellenen kullanıcıların şifreleri listede görünür.
- Eski hash kayıtlarında düz şifre bilinmediği için `eski kayıt - şifre yeniden kaydedilmeli` notu görünür.
- Varsayılan admin şifresi hash ile gerçekten eşleşiyorsa listede gösterilebilir.


## FIX37 Notu

Ölçüm Girişi ekranına `Göz No` alanı eklendi:
- Zorunlu değildir.
- Boş bırakılabilir.
- Ölçüm kayıtlarına `EyeNo` kolonu olarak yazılır.
- Ölçüm Geçmişi ekranında `Göz No` kolonu görünür.

Ölçüm kaydı sonrası Görsel Kontrol penceresi açılır:
- Çapak
- Çöküntü
- Çatlak
- Eksik Baskı
- Sıyırma
- Delik
- Oring yuva kontrolü
- Dişli Yatakları Uygunluğu
- Yüzeyde şekil bozukluğu
- Renk bozukluğu
- İtici izi
- Mıknatıs yuva kontrolü
- Kullanılan malzeme kontrolü
- Dış çapı kontrolü
- Taş yatağı kontrolü
- Balans kontrolü
- Çanak derinlik kontrolü
- Diş sayısı kontrolü
- Sıcak su malzeme kontrolü
- Diğer

Görsel kontrol kayıtları ayrı CSV dosyasına yazılır:
- `Data/VisualControlRecords.csv`

Görsel kontrol penceresinde seçilen kontrol için OK/NOK sonucu girilmelidir.


## FIX38 Notu

Derleme hatası düzeltildi:
- `FrmVisualControl.vb` içinde `For Each name In ...` satırı VB.NET tarafından formun `Name` özelliğiyle çakışıyordu.
- Döngü değişkeni `controlName As String` olarak değiştirildi.
- Görsel Kontrol penceresinin derlenmesini engelleyen BC30039 hatası giderildi.


## FIX39 Notu

Görsel Kontrol penceresi yeniden düzenlendi:
- `Seç` checkbox kolonu kaldırıldı.
- Satır kaydı için artık yalnızca `Sonuç` seçmek yeterlidir.
- Sonuç seçenekleri `UYGUN` ve `UYGUNSUZ` olarak değiştirildi.
- Boş bırakılan satırlar kaydedilmez.
- `Tümünü Uygun Yap` ve `Temizle` butonları eklendi.
- Pencere yüksekliği ve grid satırları, görsel kontrol listesinin pencereye daha iyi sığması için düzenlendi.


## FIX40 Notu

Ölçüm Geçmişi ekranına görsel kontrol sonuçları eklendi:
- `Görsel Sonuç` kolonu eklendi.
  - `UYGUN`: Görsel kontrolde uygunsuz kayıt yok.
  - `UYGUNSUZ`: En az bir görsel kontrol uygunsuz.
  - `YOK`: Bu ölçüm kaydı için görsel kontrol kaydı yok.
- `Geçmeyen Görsel Kontroller` kolonu eklendi.
  - Uygunsuz olan görsel kontrol adları bu kolonda listelenir.
- `Görsel Sonuç` filtresi eklendi:
  - TÜMÜ
  - UYGUN
  - UYGUNSUZ
  - YOK
- Genel arama artık görsel kontrol sonucu ve geçmeyen görsel kontrol adlarında da arama yapar.


## FIX41 Notu

Ölçüm Geçmişi ekranında CSV dosya konumları ve `CSV Klasörü` butonu hiçbir kullanıcıya gösterilmez.
USER veya MANAGER rolündeki kullanıcılar bu butonu görmez.


## FIX42 Notu

Ürün / Teknik Resim Yönetimi ekranına ADMIN için silme özelliği eklendi:
- `Seçili Kaydı Sil` butonu yalnızca ADMIN kullanıcısında görünür.
- Seçili TR / Revizyon kaydını `Products.csv` içinden siler.
- Aynı TR / Revizyon için tanımlı kontrol ölçülerini `ControlPoints.csv` içinden siler.
- Ölçüm geçmişi ve görsel kontrol geçmişi silinmez.
- Şifreli PDF dosyasını silmek isteyip istemediği ayrıca sorulur.
- Şifreli PDF dosyası başka bir kayıt tarafından kullanılıyorsa silinmez.


## FIX43 Notu

Ölçüm Girişi ekranında `İş Emri No` alanı opsiyonel hale getirildi:
- İş Emri No boş bırakılabilir.
- Boş bırakıldığında ölçüm kaydı yine alınır.
- CSV kaydında LotNo alanı boş yazılır.
- Görsel Kontrol penceresi de boş İş Emri No ile açılabilir.


## FIX44 Notu

Teknik resim görüntüleme alanına 90 derece döndürme eklendi:
- `Sol 90` butonu teknik resmi 90° sola çevirir.
- `Sağ 90` butonu teknik resmi 90° sağa çevirir.
- Döndürme sonrası görüntü otomatik `Fit` yapılır.
- Kontrol Ölçüleri ekranında X/Y yakalama, döndürülmüş görüntüde tıklansa bile orijinal koordinat sistemine göre kaydedilir.
- Ölçüm Girişi ekranında ölçü balonları döndürülmüş görüntü üzerinde doğru noktaya taşınır.
- Bu özellik Kontrol Ölçüleri ve Ölçüm Girişi ekranlarına eklendi.


## FIX45 Notu

Görsel Kontrol penceresi güncellendi:
- `Kaydetmeden Geç` butonu kaldırıldı.
- Sonuç seçenekleri `UYGUN` ve `UYGUN DEĞİL` olarak düzenlendi.
- UYGUN satırları yeşil, UYGUN DEĞİL satırları kırmızı görünür.
- Pencere X ile kapatılırsa Ölçüm Girişi ekranına geri dönülür.

Ölçüm Geçmişi ekranı güncellendi:
- Görsel kontrol notları `Görsel Not` kolonu olarak eklendi.
- Görsel kontrolde yazılan notlar ilgili ölçüm kaydında görünür.
- Görsel sonuç filtreleri `UYGUN`, `UYGUN DEĞİL`, `YOK` mantığına göre güncellendi.
- Eski kayıtlardaki `UYGUNSUZ` / `NOK` değerleri de `UYGUN DEĞİL` olarak değerlendirilir.


## FIX46 Notu

Ölçüm Girişi ekranında göz sıralama mantığı eklendi:
- TR / Revizyon seçiminin yanında `Göz Adedi` alanı eklendi.
- `Göz No` alanı otomatik ve salt okunur hale getirildi.
- Varsayılan Göz Adedi = 1, Göz No = 1.
- Ölçüm kaydedildikten sonra Göz No otomatik olarak bir sonraki değere geçer.
  Örnek: Göz Adedi 4 ise kayıt sırası 1 → 2 → 3 → 4 şeklindedir.
- Son göz kaydedildiğinde kullanıcıya tamamlandı uyarısı verilir.
- Yeni çevrim/ürün için `Temizle / Göz 1` butonu ile sıra tekrar 1'e alınabilir.
- Ölçüm CSV dosyasına `EyeCount` ve `EyeNo` alanları yazılır.
- Görsel kontrol CSV dosyasına da `EyeCount` ve `EyeNo` alanları yazılır.
- Ölçüm Geçmişi ekranına `Göz Adedi` kolonu eklendi.


## FIX47 Notu

Ölçüm Girişi ekranına `Göz Kapalı` seçeneği eklendi:
- Kullanıcı mevcut Göz No için `Göz Kapalı` seçebilir.
- Göz kapalı seçilirse o göz için ölçüm kaydı alınmaz.
- Göz kapalı seçilirse görsel kontrol penceresi açılmaz.
- Göz kapalı bilgisi ayrı CSV dosyasına yazılır:
  - `Data/ClosedEyeRecords.csv`
- Kapalı göz kaydedildikten sonra Göz No otomatik olarak sıradaki göze geçer.
- Bu işlem audit log'a `EYE_CLOSED_SKIP` olarak yazılır.


## FIX48 Notu

Ölçüm Geçmişi ekranı güncellendi:
- `ClosedEyeRecords.csv` içindeki kapalı göz kayıtları da Ölçüm Geçmişi listesinde görünür.
- Kapalı göz kayıtları `GÖZ KAPALI` sonucu ile listelenir.
- Ölçüm Sonucu filtresine `GÖZ KAPALI` eklendi.
- Görsel Sonuç filtresine `GÖZ KAPALI` eklendi.
- Kapalı göz satırları açık mavi renkte gösterilir.

Teknik resim ile geçmiş kayıt inceleme eklendi:
- Ölçüm Geçmişi listesinde bir satıra çift tıklanınca teknik resim inceleme penceresi açılır.
- Açılan pencerede solda teknik resim, sağda aynı kayıt numarasına ait ölçüm değerleri görünür.
- Teknik resim üzerinde ölçü noktaları renkli balonlarla gösterilir:
  - OK: yeşil
  - NOK: kırmızı
  - HATALI: sarı
- Çift tıklanan ölçü balonu sarı çerçeveyle vurgulanır.
- Kapalı göz kaydına çift tıklanırsa teknik resim açılır, sağ tarafta `GÖZ KAPALI` bilgisi gösterilir.


## FIX49 Notu

Kontrol Ölçüleri ekranı güncellendi:
- Teknik resim üzerinde tanımlı X/Y noktaları balon olarak gösterilir.
- Sağ alttaki ölçü listesinden bir ölçüye tıklandığında teknik resim üzerindeki ilgili X/Y noktası vurgulanır.
- Seçili ölçünün X % ve Y % bilgisi üst bilgi satırında gösterilir.
- Teknik resim üzerindeki ölçü balonuna tıklanınca sağdaki listede ilgili ölçü seçilir ve form alanları doldurulur.
- Pasif ölçüler gri balonla gösterilir.
- Döndürme sırasında ölçü balonları doğru konuma taşınır.


## FIX50 Notu

Ölçüm Girişi ekranında gözler arası geçiş eklendi:
- Göz No alanının yanına `◀` ve `▶` butonları eklendi.
- Kullanıcı Göz Adedi içindeki gözler arasında ileri/geri geçiş yapabilir.
- Her gözün girilmiş ölçüm değerleri ekranda geçici olarak ayrı ayrı tutulur.
- Örnek kullanım:
  - Göz 1'de sadece bir ölçü girilir.
  - `▶` ile Göz 2'ye geçilir.
  - Aynı ölçü Göz 2 için girilir.
  - `▶` ile diğer gözlere geçilir.
  - Daha sonra `◀` / `▶` ile geri dönülerek diğer ölçüler tamamlanır.
- Ölçüm Kaydet yalnızca aktif Göz No için kayıt alır.
- Kayıt sonrası aktif gözün geçici değerleri temizlenir; sıradaki gözün varsa daha önce girilmiş değerleri geri yüklenir.
- `Göz Kapalı` bilgisi de göz bazında geçici olarak hatırlanır.
- `Temizle / Göz 1` yeni çevrime başlar ve tüm geçici göz değerlerini temizler.


## FIX51 Notu

Kontrol Ölçüleri ekranında teknik resim üzerindeki ölçü işaretleri güncellendi:
- Ölçü noktaları artık balon yerine `kırmızı nokta` olarak gösterilir.
- Pasif ölçüler gri nokta olarak görünür.
- Seçili ölçü noktası daha büyük görünür ve sarı halka ile vurgulanır.
- Noktaların üzerinde ölçü numarası görünmez; bilgi tooltip içinde korunur.


## FIX52 Notu

Kontrol Ölçüleri ekranında seçili ölçünün teknik resim üzerinde görünmesi güçlendirildi:
- Sağ alttaki listeden ölçü seçildiğinde teknik resim otomatik açılır.
- Seçili ölçünün X/Y noktası teknik resim üzerinde büyük kırmızı nokta ve sarı halka ile gösterilir.
- Teknik resim zaten açıksa doğrudan ilgili noktaya odaklanır.
- Teknik resim kapalıysa önce PDF açılır, yükleme tamamlanınca seçili nokta gösterilir.
- 90° döndürme sonrası seçili nokta doğru konumda kalır.


## FIX53 Notu

Ölçüm Girişi ekranında Göz No sınırı düzeltildi:
- `Göz No`, `Göz Adedi` değerini geçemez.
- Son göz kaydedildiğinde Göz No artık `Göz Adedi + 1` olmaz.
- Örneğin Göz Adedi = 1 ise kayıt sonrası Göz No yine 1 olarak kalır.
- Göz No yanlışlıkla büyük değere çıkarsa otomatik olarak Göz Adedi değerine çekilir.


## FIX54 Notu

Ölçüm Kaydı / Teknik Resim İnceleme ekranına da teknik resim döndürme desteği eklendi:
- Sol panel üstüne PDF kontrol araç çubuğu eklendi.
- Fit, yakınlaştırma, kaydırma ve `Sol 90` / `Sağ 90` döndürme aktif hale getirildi.
- Ölçüm işaretleri döndürülmüş teknik resimle birlikte aynı konum mantığıyla görünür.


## FIX55 Notu

Ekranlarda görünen `Lot No` ifadesi `İş Emri No` olarak değiştirildi:
- Ölçüm Girişi ekranı
- Ölçüm Geçmişi filtre ve kolon başlıkları
- Görsel Kontrol penceresi
- Ölçüm Kaydı / teknik resim inceleme penceresi

Not: Eski kayıtlarla uyumluluk bozulmaması için CSV içindeki teknik alan adı `LotNo` olarak korunmuştur.


## FIX56 Notu

Ölçüm Girişi ekranında `İş Emri No` etiketi ile input alanı üst üste gelmeyecek şekilde hizalama düzeltildi:
- `İş Emri No` etiketi sola alındı.
- İş Emri No input alanı sağa kaydırıldı.
- Seri No alanı da aynı satırda çakışmayacak şekilde yeniden hizalandı.


## FIX57 Notu

Ölçüm Girişi ekranındaki üst bilgi alanı gözden geçirilerek yeniden hizalandı:
- TR / Revizyon ve TR Filtre alanları aynı başlangıç ve genişlikte hizalandı.
- Göz Adedi, Göz No ve ileri/geri göz geçiş butonları daha düzenli yerleştirildi.
- İş Emri No etiketi ve input alanı çakışmayacak şekilde ayrıldı.
- Seri No alanı sağ tarafa hizalandı.
- Göz Kapalı, Operatör/Bilgisayar bilgisi ve butonlar alt satırda daha temiz yerleştirildi.


## FIX58 Notu

Kontrol Ölçüleri ekranına ürün/kalıp bilgileri eklendi:
- TR Kodu
- Plastik Kodu
- Ürün Adı
- Malzeme
- Kalıp Göz Adedi
- Kalıp Kodu

Bu bilgiler `Ürün Bilgilerini Kaydet` butonu ile ürün/teknik resim kaydına yazılır.
Ayrıca bu bilgiler:
- Ölçüm Girişi ekranında üst bilgi alanında gösterilir.
- Ölçüm Geçmişi ekranında kolon olarak gösterilir ve genel aramaya dahil edilir.
- Ölçüm Kaydı / teknik resim inceleme penceresinde gösterilir.

Not: Bilgiler `Products.csv` içine yeni kolonlar olarak eklenir. Eski CSV kayıtları uygulama açıldığında otomatik olarak bu kolonlara uyumlu hale gelir.


## FIX59 Notu

Ürün/kalıp bilgilerine `Renk` alanı eklendi:
- Kontrol Ölçüleri ekranında Renk girilebilir.
- Renk bilgisi `Ürün Bilgilerini Kaydet` butonu ile Products.csv dosyasına yazılır.
- Ölçüm Girişi ekranında Renk bilgisi gösterilir.
- Ölçüm Geçmişi ekranında Renk kolonu eklendi ve genel aramaya dahil edildi.
- Ölçüm Kaydı / teknik resim inceleme penceresinde Renk bilgisi gösterilir.

Not: CSV tarafında alan adı `ColorName` olarak tutulur.


## FIX60 Notu

Görsel Kontrol penceresine malzeme ve renk kontrolü eklendi:
- `Malzeme bilgisi kontrolü` satırı eklendi.
- `Renk bilgisi kontrolü` satırı eklendi.
- Beklenen Malzeme ve Renk bilgisi üst bilgi alanında gösterilir.
- Malzeme/Renk kontrol satırlarında Not alanına beklenen değer otomatik yazılır.
- Bu kontroller UYGUN / UYGUN DEĞİL olarak kaydedilir ve Ölçüm Geçmişi ekranında görsel kontrol sonucu/notları içinde görünür.


## FIX61 Notu

Görsel Kontrol penceresinde malzeme ve renk kontrolü zorunlu hale getirildi:
- `Malzeme bilgisi kontrolü` için UYGUN veya UYGUN DEĞİL seçilmeden kayıt tamamlanamaz.
- `Renk bilgisi kontrolü` için UYGUN veya UYGUN DEĞİL seçilmeden kayıt tamamlanamaz.
- Sonuç hücresine tıklayınca seçim hızlı şekilde UYGUN / UYGUN DEĞİL arasında değişir.
- Alt butonlara `Seçili Uygun` ve `Seçili Uygun Değil` eklendi.
- Klavyeden Space/Enter = UYGUN, N = UYGUN DEĞİL olarak kullanılabilir.


## FIX62 Notu

Ölçüm Kaydı / Teknik Resim İnceleme penceresi güncellendi:
- Sağ panel artık iki bölümden oluşur:
  - Ölçüm Sonuçları
  - Görsel Kontrol Sonuçları
- Ölçüm Geçmişi listesinden bir ölçüme çift tıklanınca açılan pencerede ilgili RecordId'ye ait görsel kontrol kayıtları da gösterilir.
- Görsel kontrol satırlarında Kontrol Tarihi, Görsel Kontrol, Sonuç ve Not kolonları gösterilir.
- UYGUN satırları yeşil, UYGUN DEĞİL satırları kırmızı renkte görünür.
- Görsel kontrol kaydı yoksa pencerede `Görsel kontrol kaydı yok` bilgisi gösterilir.


## FIX63 Notu

Ölçüm Girişi ekranında Göz Adedi otomatik ürün bilgisinden alınır:
- Seçili ürün/teknik resim kaydında `Kalıp Göz Adedi` tanımlıysa, `Göz Adedi` alanı otomatik olarak bu değer olur.
- Örneğin Kalıp Göz Adedi = 8 ise Ölçüm Girişi ekranında Göz Adedi otomatik 8 gelir.
- Kalıp Göz Adedi boş veya geçersizse Göz Adedi varsayılan olarak 1 kalır.
- Ürün değiştirildiğinde göz sırası tekrar 1’den başlar.


## FIX64 Notu

Ölçüm kaydetme mantığı tüm gözleri kapsayacak şekilde değiştirildi:
- Ölçüm Girişi ekranındaki kayıt butonu `Tüm Gözleri Kaydet` olarak değiştirildi.
- Kayıt öncesinde tüm gözlerin zorunlu ölçüleri kontrol edilir.
- Eksik veya hatalı ölçüm varsa ilgili Göz No otomatik açılır ve kullanıcı uyarılır.
- Kayıt tamamlandığında Ölçüm Geçmişi'ne tüm göz numaraları için ayrı ayrı ölçüm kayıtları yazılır.
- Her göz numarası için ayrı RecordId oluşturulur.
- Her açık göz için Görsel Kontrol penceresi ayrı ayrı açılır.
- Kapalı gözler `Göz Kapalı` olarak kaydedilir ve görsel kontrol sorulmaz.

Görsel Kontrol penceresi daha kullanıcı dostu olacak şekilde yeniden düzenlendi:
- Daha geniş pencere ve daha okunabilir üst bilgi alanı.
- Göz No bilgisi başlıkta daha belirgin hale getirildi.
- Beklenen Malzeme/Renk bilgisi daha görünür hale getirildi.
- Alt butonlar yeniden düzenlendi.
- `Malzeme/Renk Uygun` hızlı seçim butonu eklendi.
- Satır yüksekliği ve kolon oranları daha rahat seçilecek şekilde düzenlendi.


## FIX65 Notu

Üretim bildirimi ve kalite ticket sistemi eklendi:
- Ana menüye `Üretim Bildirimi Oluştur` penceresi eklendi.
- Ana menüye açık ticket sayısını gösteren `Kalite Ticketları` penceresi eklendi.
- Üretim bölümü; Makine, Kalıp Kodu, TR/Revizyon, bağlanan Hammadde, İş Emri No ve Not bilgilerini girerek ticket oluşturabilir.
- Ürün seçildiğinde Ürün Adı, tanımlı Malzeme, Renk, Plastik Kodu, Kalıp Kodu ve Kalıp Göz Adedi bilgileri otomatik gösterilir.
- Kalıp Kodu ve Hammadde alanları ürün bilgisinden otomatik doldurulur; kullanıcı isterse değiştirebilir.
- Kalite kontrol kullanıcıları ticketları `Kalite Ticketları` ekranında AÇIK / GÖRÜLDÜ / KAPALI durumlarına göre takip edebilir.
- Ticketlar `Görüldü Yap` ve `Ticketı Kapat` butonları ile yönetilebilir.
- Ticket kayıtları `Data/ProductionTickets.csv` dosyasında tutulur.
- Kalıp bağlama bitirme ve kalite ticketı oluşturma tek bütünsel işlem olarak yürütülür.
- İşlem yarıda kesilirse `Data/PendingTransactions` altındaki kalıcı işlem günlüğü sonraki program açılışında tamamlanır.
- Aynı bağlama tekrar işlense bile mevcut ticket kullanılır; mükerrer kalite ticketı oluşturulmaz.


## FIX66 Notu

FIX65 sonrası oluşan derleme hataları düzeltildi:
- FrmMain.vb içindeki açık ticket sayısı hesaplama satırı düzeltildi.
- FrmQualityTickets.vb içindeki açık/görüldü/kapalı ticket sayısı hesaplama satırları düzeltildi.
- FrmQualityTickets.vb içindeki CellDoubleClick event handler adı düzeltildi.


## FIX67 Notu

Üretim Bildirimi / Kalite Ticket Oluştur penceresi gözden geçirilip yeniden düzenlendi:
- TR / Revizyon seçimi üstte ayrı `Ürün / Teknik Resim Bilgisi` bölümüne alındı.
- TR Filtre alanı görünür ve daha geniş hale getirildi.
- Ürün bilgileri tek satıra sıkışmadan iki satır halinde gösterildi.
- Makine, Kalıp Kodu, Hammadde, İş Emri No ve Not alanları `Üretim Bağlama Bilgileri` bölümünde hizalandı.
- Form genişletildi ve input alanlarının birbiriyle çakışması engellendi.
- Butonlar ve bilgilendirme metni alt bölümde düzenlendi.


## FIX68 Notu

Üretim Bildirimi / Kalite Ticket Oluştur ekranına teknik resim görüntüleme eklendi:
- TR / Revizyon seçildikten sonra `Teknik Resim` butonu ile ilgili teknik resim açılabilir.
- Teknik resim program içindeki PDF görüntüleyicide açılır.
- Üretim kullanıcısı ticket oluşturmadan önce seçilen TR'nin teknik resmini kontrol edebilir.
- Teknik resim açma işlemi log kaydına yazılır.


## FIX69 Notu

Program içi teknik resim görüntüleyici geliştirildi:
- Fit butonu eklendi.
- Yakınlaştır / uzaklaştır butonları eklendi.
- Zoom yüzdesi gösterimi eklendi.
- Yukarı / aşağı / sol / sağ kaydırma butonları eklendi.
- Sol 90 ve Sağ 90 döndürme butonları eklendi.
- Teknik resim PDF'i yüksek kaliteli PNG olarak render edilip program içinde gösterilir.
- Üretim Bildirimi ekranındaki `Teknik Resim` butonu da bu gelişmiş görüntüleyiciyi kullanır.


## FIX70 Notu

Gelişmiş teknik resim görüntüleyici kontrol edildi ve zoom durum güncellemesi güçlendirildi:
- Fit, +, -, yön tuşları ve Sol/Sağ 90 döndürme fonksiyonları aynı görüntüleyici içinde kontrol edildi.
- Fit sonrası gerçek zoom yüzdesinin ekrandaki yüzde etiketine yansıması için WebView2 mesaj sistemi eklendi.
- İlk açılışta ve pencere boyutu değiştiğinde Fit hesaplanan zoom değerini geri bildirir.
- + / - butonları artık Fit sonrası oluşan gerçek zoom değerinin üzerinden çalışır.


## FIX71 Notu

Teknik resim görüntüleyici WebView/JavaScript bağımlılığından çıkarılarak doğrudan WinForms görüntüleyiciye çevrildi:
- Fit, +, -, yön tuşları ve Sol/Sağ 90 döndürme artık doğrudan PictureBox üzerinden çalışır.
- Zoom etiketi doğrudan uygulama içindeki gerçek zoom değerinden güncellenir.
- Kaydırma butonları AutoScroll panel üzerinden çalışır.
- Döndürme sonrası resim yeniden fit edilir.
- WebView2 script zamanlaması kaynaklı çalışmama riski kaldırıldı.


## FIX72 Notu

Üretim Bildirimi ekranından açılan teknik resim penceresinde `Harici Aç` butonu kaldırıldı:
- Üretim Bildirimi / Kalite Ticket Oluştur ekranındaki `Teknik Resim` butonu artık harici açma seçeneği göstermeden açılır.
- Diğer ekranlarda teknik resim görüntüleyici varsayılan davranışını korur.


## FIX73 Notu

Ölçüm Girişi ekranında gözler arası geçişte ölçüm bilgilerinin kaybolmaması için hafıza mekanizması güçlendirildi:
- Göz değiştirmeden önce açık ölçü giriş popup'ı varsa otomatik kaydedilir.
- Göz değiştirmeden önce aktif grid/edit durumu commit edilir.
- Aktif göz ölçüleri, sonuçları ve notları yeniden hesaplanıp ilgili Göz No hafızasına yazılır.
- Göz değiştirip geri dönüldüğünde ilgili gözün ölçüm değerleri geri yüklenir.
- Popup dışına tıklandığında ölçüm penceresi artık değeri kaybetmeden kaydeder.
- `Tüm Gözleri Kaydet` öncesinde açık popup değeri de otomatik hafızaya alınır.


## FIX74 Notu

Kalıp kaynaklı sorunlar için kalıp ticket sistemi eklendi:
- Ana menüye `Kalıp Ticketları` penceresi eklendi.
- Kalıp koduna bağlı açık/kapalı ticket takibi yapılabilir.
- Kalıp kaynaklı sorun için önem seviyesi, sorun tipi, sorun açıklaması ve aksiyon notu girilebilir.
- Sorun giderildiğinde seçili kalıp ticket `Kapat` işlemi ile kapatılabilir.
- Ticketlar `Data/MoldTickets.csv` dosyasında tutulur.
- Üretim Bildirimi ekranında kalıp kodu girildiğinde veya ürün seçildiğinde o kalıba ait açık ticket kontrol edilir.
- Üretim kalıp bağlarken açık kalıp ticket varsa kırmızı uyarı görür.
- Üretim ekranındaki `Kalıp Ticketları` butonu ile ilgili kalıbın ticket listesi açılabilir.


## FIX75 Notu

Kalıp ticket ve üretim bildirimi ekranlarında TR'ye göre kalıp seçimi iyileştirildi:
- Kalıp Ticketları ekranında TR / Revizyon seçilince Kalıp Kodu alanı otomatik dolar.
- Kalıp Kodu alanı artık açılır liste gibi çalışır.
- Bir TR birden fazla kalıpta basılıyorsa, ürün bilgisindeki Kalıp Kodu alanına kalıplar `;`, `/`, `,` veya `|` ile ayrılarak yazılabilir.
  - Örnek: `K525; K1572; K1880`
- Bu durumda Kalıp Ticketları ekranında doğru kalıp listeden seçilebilir.
- Üretim Bildirimi ekranında da aynı mantık eklendi; üretim hangi kalıbı bağladıysa onu seçebilir.
- Üretim ekranındaki açık kalıp ticket uyarısı seçilen Kalıp Kodu'na göre çalışır.

Çözüm mantığı:
TR tek başına kalıp ticket için yeterli anahtar değildir. Doğru takip anahtarı `TR Kodu + Kalıp Kodu` olmalıdır.


## FIX76 Notu

Kalite Ticketları entegrasyonu güncellendi:
- Ana menüdeki `Kalite Ticketları` butonu açık ticket sayısını güncel gösterir.
- Açık kalite ticket varsa buton kırmızı renkte görünür.
- Açık ticket sayısı ana menü aktif oldukça otomatik yenilenir.
- Kalite Ticketları ekranına `Kontrol Girişi Aç` butonu eklendi.
- Açık/Görüldü durumundaki kalite ticket seçilip `Kontrol Girişi Aç` dendiğinde ilgili TR / Revizyon için Ölçüm Girişi ekranı otomatik açılır.
- Ticket satırına çift tıklayınca da ilgili TR için Ölçüm Girişi açılır.
- Açık ticket üzerinden Kontrol Girişi açıldığında ticket otomatik `GÖRÜLDÜ` durumuna alınır.
- Ölçüm Girişi ekranı artık dışarıdan TR / Revizyon parametresi alarak ilgili ürünü otomatik seçebilir.


## FIX77 Notu

Kalıp bağlama bildirimi ekranı isimlendirme ve kapanış davranışı güncellendi:
- Ana menüdeki `Üretim Bildirimi Oluştur` butonu `Kalıp Bağlama Bildirimi Oluştur` olarak değiştirildi.
- İlgili pencere başlığı da kalıp bağlama bildirimi mantığına göre güncellendi.
- Ticket oluşturulduktan sonra bildirim penceresi otomatik kapanır.


## FIX78 Notu

Kalite ticket görünürlüğü güncellendi:
- `AÇIK` ve `GÖRÜLDÜ` durumundaki kalite ticketlar aktif kabul edilir.
- Ana menüdeki `Kalite Ticketları` butonu artık aktif ticket sayısını gösterir.
- Aktif ticket varsa buton kırmızı görünür.
- Kalite Ticketları ekranında varsayılan durum filtresi `AKTİF` yapıldı.
- `AKTİF` filtresi hem `AÇIK` hem de `GÖRÜLDÜ` ticketları birlikte gösterir.
- `KAPALI` ticketlar varsayılan aktif listede gösterilmez.


## FIX79 Notu

Ticket üzerinden Ölçüm Girişi tamamlandığında ticket otomatik kapatma eklendi:
- Kalite Ticketları ekranından `Kontrol Girişi Aç` ile açılan Ölçüm Girişi artık ticket bağlantısını taşır.
- Ölçüm kaydı başarıyla tamamlandığında ilgili kalite ticket otomatik `KAPALI` durumuna alınır.
- Kapanış notu otomatik yazılır: `Ticket üzerinden ölçüm girişi tamamlandı. Otomatik kapatıldı.`
- Otomatik kapatma işlemi log kaydına yazılır.


## FIX80 Notu

Kapalı kalite ticket üzerinden ölçüm sonuçlarını gösterme eklendi:
- Ticket üzerinden yapılan ölçümlerde `ProductionTicketId` bilgisi ölçüm kayıtlarına yazılır.
- Kapalı ticket satırına çift tıklanınca artık Kontrol Girişi açılmaz; o ticketın ölçüm sonuçları listelenir.
- Kalite Ticketları ekranına `Ticket Ölçümleri` butonu eklendi.
- Ticket ölçüm listesinde kayıt no, TR, revizyon, iş emri, seri, göz no, ölçü satırı, NOK sayısı ve görsel kontrol durumu gösterilir.
- Ölçüm kaydına çift tıklayınca mevcut Ölçüm Kaydı inceleme penceresi açılır.
- Eski ticketlarda `ProductionTicketId` yoksa, program TR / Revizyon ve ticket tarih aralığına göre ölçüm kaydı arar.


## FIX81 Notu

Kontrol Ölçüleri ekranında `Ölçü Adı` alanı opsiyonel yapıldı:
- `Ölçü Adı zorunludur` kontrolü kaldırıldı.
- Ölçü Adı boş bırakılarak kontrol ölçüsü kaydedilebilir.
- Ekrandaki etiket `Ölçü Adı (ops.)` olarak güncellendi.


## FIX82 Notu

Kontrol Ölçüleri ekranında `Ölçü Adı (ops.)` alanına gri yer tutucu metin eklendi:
- Alan boşken kutu içinde `Opsiyonel` görünür.
- Görünüm, TR filtre alanındaki placeholder davranışı ile aynı hale getirildi.


## FIX83 Notu

Ölçüm Kaydı - Teknik Resim Üzerinde İnceleme ekranında Fit / Zoom davranışı düzeltildi:
- Fit butonu artık yüzde değerini yapay olarak 100'e çekmez.
- Fit sonrası hesaplanan gerçek zoom yüzdesi ekrandaki yüzde alanına yansır.
- + / - butonları artık Fit sonrası oluşan gerçek zoom değerinin üzerinden çalışır.
- Sol 90 / Sağ 90 sonrası oluşan Fit zoom yüzdesi de doğru güncellenir.
- WebView2 içindeki görüntüleyiciden VB tarafına zoom geri bildirimi eklendi.


## FIX84 Notu

Ana ekrana `Kullanıcı Değiştir` özelliği eklendi:
- Ana menüye `Kullanıcı Değiştir` butonu eklendi.
- Butona basıldığında giriş ekranı tekrar açılır.
- Yeni kullanıcı başarılı giriş yaparsa ana ekran aynı pencere içinde yeni kullanıcıya göre yenilenir.
- Admin / kullanıcı yetkilerine göre menü butonları yeniden oluşturulur.
- Kullanıcı değişimi log kaydına yazılır.
- Kullanıcı değiştirirken program tamamen kapanıp açılmaz.


## FIX85 Notu

Rol yapısı güncellendi:
- Kullanıcı Yönetimi ekranında rol seçenekleri şu hale getirildi:
  - Üretim Kullanıcısı
  - Plastikhane Kalite Kontrol
  - Yönetici
  - Admin
- Eski `USER`, `MANAGER`, `ADMIN` kayıtları girişte otomatik yeni rol karşılığına çevrilir.
- Ana ekran menüsü role göre yeniden düzenlenir:
  - Üretim Kullanıcısı: Kalıp Bağlama Bildirimi ve Kalıp Ticketları
  - Plastikhane Kalite Kontrol: Ölçüm Girişi, Ölçüm Geçmişi, Kalite Ticketları ve Kalıp Ticketları
  - Yönetici: Operasyon ekranları + Ürün/Teknik Resim Yönetimi + Kontrol Ölçüleri
  - Admin: tüm ekranlar + Kullanıcı Yönetimi + Log Kayıtları


## FIX86 Notu

Yönetici rolünün yetkisi güncellendi:
- Yönetici artık `Ürün / Teknik Resim Yönetimi` ekranına ulaşamaz.
- Yönetici artık `Kontrol Ölçüleri` ekranına ulaşamaz.
- Bu iki ekran yalnızca `Admin` rolüne bırakıldı.


## FIX87 Notu

Rol yapısı tekrar güncellendi:
- `Üretim Yöneticisi` rolü eklendi.
- `Kalite Kontrol Yöneticisi` rolü eklendi.
- Kullanıcı Yönetimi ekranındaki rol listesi şu hale getirildi:
  - Üretim Kullanıcısı
  - Üretim Yöneticisi
  - Plastikhane Kalite Kontrol
  - Kalite Kontrol Yöneticisi
  - Yönetici
  - Admin
- Üretim Yöneticisi üretim tarafı ekranlarına ulaşır.
- Kalite Kontrol Yöneticisi kalite tarafı ekranlarına ulaşır.
- Ürün / Teknik Resim Yönetimi ve Kontrol Ölçüleri ekranları yalnızca Admin rolünde kalır.


## FIX88 Notu

Üretim Yöneticisi rolünün yetkisi güncellendi:
- Üretim Yöneticisi artık ana ekranda `Kalite Ticketları` butonunu görebilir.
- Kalite Ticketları ekranına girip açık/görüldü/kapalı ticketları takip edebilir.
- Diğer yönetim ekranları yine Admin yetkisinde kalır.


## FIX89 Notu

Üretim Yöneticisi için Kalite Ticketları yetkisi güncellendi:
- Üretim Yöneticisi `Kalite Ticketları` ekranını görmeye devam eder.
- Üretim Yöneticisi bu ekranda ölçüm girişi yapamaz.
- Üretim Yöneticisi için `Kontrol Girişi Aç` butonu gösterilmez.
- Üretim Yöneticisi için `Görüldü Yap` ve `Ticketı Kapat` butonları gösterilmez; bu işlemler servis seviyesinde de engellenir.
- Üretim Yöneticisi aktif ticket satırına çift tıklarsa ölçüm girişi açılmaz, yetki uyarısı gösterilir.
- Kapalı ticketlarda `Ticket Ölçümleri` görüntüleme davranışı korunur.


## FIX90 Notu

Kalite Kontrol Ticketları ekranında CSV dosya yolu gizlendi:
- Ekrandaki `CSV: ...ProductionTickets.csv` yolu artık görünmez.
- CSV dosya konumları ve `CSV Klasörü` butonları Admin dahil hiçbir kullanıcıya gösterilmez.


## FIX91 Notu

Kalite kontrol ölçüm/görsel kontrol sonucunda uygunsuzluk varsa üretime otomatik ticket açma eklendi:
- Ölçüm sonucunda `NOK` varsa üretime `Üretim Ticketları` ekranında ticket açılır.
- Görsel kontrolde `UYGUN DEĞİL / UYGUNSUZ / NOK` varsa üretime ticket açılır.
- Aynı ölçüm kayıt no için mükerrer üretim ticketı açılmaz.
- Ana ekrana `Üretim Ticketları` butonu eklendi.
- Üretim ticketları `QualityToProductionTickets.csv` dosyasında tutulur.
- Üretim kullanıcıları/yöneticileri bu ticketları görebilir, görüldü yapabilir, ölçüm kaydını açabilir ve kapatabilir.


## FIX92 Notu

Üretim Yöneticisi için `Kalıp Bağlama Dashboardu` eklendi:
- Ana ekrana `Kalıp Bağlama Dashboardu` butonu eklendi.
- Bu ekran `Üretim Yöneticisi`, `Yönetici` ve `Admin` rollerinde görünür.
- Dashboard verisi `ProductionTickets.csv` içindeki kalıp bağlama bildirimlerinden okunur.
- Günlük / Haftalık / Aylık / Tümü dönem seçimi eklendi.
- Kim ne kadar kalıp bağladı özeti eklendi.
- Hangi kalıbı kim bağladı özeti eklendi.
- Detay listede tarih, kullanıcı, makine, kalıp, TR, ürün, hammadde ve iş emri bilgileri gösterilir.


## FIX93 Notu

Kullanıcı oturumu ve 10 dakika pasif kalma kontrolü eklendi:
- Aynı kullanıcı farklı bilgisayarlardan aynı anda giriş yapamaz.
- Kullanıcı başka bilgisayarda açıksa giriş ekranında hangi bilgisayarda açık olduğu gösterilir.
- Kullanıcı 10 dakika programda işlem yapmazsa program otomatik kapatılır.
- Klavye ve mouse hareketleri aktivite olarak sayılır.
- Oturum bilgileri `ActiveSessions.csv` dosyasında tutulur.
- Program normal kapanınca veya kullanıcı değiştirince aktif oturum kaydı temizlenir.
- Program çökmesi / elektrik kesintisi gibi durumda kalan oturum kayıtları 10 dakika sonra kilit sayılmaz.


## FIX94 Notu

Kalıp bağlama başlangıç / bitiş takibi entegre edildi:
- `Kalıp Bağlama Bildirimi Oluştur` ekranı `Kalıp Bağlama Takibi` mantığına çevrildi.
- Üretim kullanıcısı önce `Bağlamayı Başlat` ile kalıp bağlama başlangıç saatini kaydeder.
- Devam eden bağlamalar ekranda listelenir.
- Bağlama bittiğinde seçili kayıt için `Bağlamayı Bitir + Kalite Ticket Aç` kullanılır.
- Bitiş yapılınca bitiş saati, bitiren kullanıcı ve süre kaydedilir.
- Bitiş yapılınca Kalite Kontrol için otomatik `Kalite Ticketı` oluşturulur.
- Yeni takip dosyası: `MoldBindingRecords.csv`
- Kalite ticketına `BindingId`, `BindingStartAt`, `BindingEndAt`, `BindingDurationMin` bilgileri de yazılır.
- `Kalıp Bağlama Dashboardu` artık başlangıç / bitiş / süre / devam eden kayıtları `MoldBindingRecords.csv` üzerinden gösterir.


## FIX95 Notu

Kontrol Ölçüleri ekranında etiket düzeltildi:
- `Ölçü Adı (ops.)` yazısı `Ölçü Adı` olarak değiştirildi.
- Alan yine zorunlu değildir.
- TextBox içindeki `Opsiyonel` placeholder bilgisi korunur.


## FIX96 Notu

Kalıp Bağlama Takibi ekranında pencere kapanış davranışı güncellendi:
- `Bağlamayı Başlat` başarılı olduğunda pencere otomatik kapanır.
- `Bağlamayı Bitir + Kalite Ticket Aç` başarılı olduğunda pencere otomatik kapanır.
- Hata / eksik bilgi durumlarında pencere açık kalır.


## FIX97 Notu

Ana ekranda aktif kalıp bağlama uyarısı eklendi:
- `Kalıp Bağlama Bildirimi Oluştur` butonu artık devam eden kalıp bağlama varsa kırmızı görünür.
- Buton üzerinde aktif bağlanmakta olan kalıp sayısı gösterilir.
- Sayı `MoldBindingRecords.csv` içinde `STARTED` durumundaki kayıtlardan hesaplanır.
- Devam eden kayıt kalmazsa buton tekrar normal beyaz görünür.


## FIX98 Notu

Ana ekrandaki aktif kalıp bağlama buton metni düzenlendi:
- `Kalıp Bağlama Bildirimi Oluştur` birinci satırda gösterilir.
- `(X kalıp)` bilgisi ikinci satırda gösterilir.


## FIX99 Notu

Üretim Kullanıcısı için ticket ekranları sadece görüntüleme moduna alındı:
- Üretim Kullanıcısı `Üretim Ticketları` ekranını görür; ancak görüldü yapamaz ve kapatamaz.
- Üretim Kullanıcısı `Üretim Ticketları` ekranında ölçüm kaydını açıp inceleyebilir; bu işlem ticket durumunu değiştirmez.
- Üretim Kullanıcısı `Kalıp Ticketları` ekranını görür; ancak yeni ticket açamaz ve ticket kapatamaz.
- Üretim Kullanıcısı için ilgili ekranlarda `Sadece görüntüleme` bilgisi gösterilir.
- Üretim Yöneticisi ve üst roller için müdahale yetkileri korunur.


## FIX100 Notu

Program ikonu entegre edildi:
- Yeni sade teknik resim / ölçüm temalı ikon `Resources/app_icon.ico` olarak eklendi.
- PNG kaynak dosyası `Resources/app_icon.png` olarak eklendi.
- Proje dosyasında `ApplicationIcon` yeni ikonla güncellendi.
- Form pencereleri için ikon uygulama servisi eklendi.
- Giriş, ana ekran ve alt pencerelerde aynı ikonun görünmesi sağlandı.


## FIX101 Notu

Admin için Program Güncelleme Sihirbazı eklendi:
- Ana ekranda Admin menüsüne `Program Güncelleme Sihirbazı` butonu eklendi.
- Güncelleme paketi `.zip` olarak seçilebilir.
- Güncelleme başlatıldığında program kapanır ve harici bir PowerShell/BAT güncelleme işlemi çalışır.
- Mevcut program dosyaları `Backups` klasörüne yedeklenir.
- `Data`, `Drawings`, `Temp`, `Backups` ve `Updates` klasörleri korunur.
- Güncelleme tamamlanınca `TeknikResimOlcum.exe` yeniden başlatılmaya çalışılır.
- Güncelleme logu `Temp/ProgramUpdate_.../update_log.txt` içine yazılır.
- `Updates` ve `Backups` klasör yolları AppPaths içine eklendi.


## FIX103 Notu

Kalıp bağlama süreci geliştirildi:
- Aynı kalıp kodu `STARTED` durumundayken ikinci kez bağlamaya başlatılamaz.
- Kalıp son bağlandığı makineden farklı bir makineye bağlanıyorsa `Makine Değişim Nedeni` zorunlu hale getirildi.
- Kalıp bağlama kaydına önceki makine, bağlama nedeni, makine değişim nedeni, başlangıç notu, bitiş notu ve süre bilgileri eklendi.
- Kalite ticketına da bağlama nedeni, önceki makine ve makine değişim nedeni bilgileri yazılır.
- Kalıp Bağlama Dashboardu; makine değişimi sayısı, önceki makine, bağlama nedeni, makine değişim nedeni, başlangıç/bitiş notları ve süre bilgilerini gösterecek şekilde genişletildi.


## FIX104 Notu

Ana ekran görsel olarak geliştirildi:
- Üst bilgi alanı yenilendi.
- Aktif ticket ve bağlama sayıları için özet kartlar eklendi.
- Kategori panelleri daha düzenli hale getirildi.
- Butonlar sadeleştirildi ve görsel uyarı renkleri korundu.
- Ana ekran daha modern ve okunabilir bir düzene taşındı.

## SECURITY FIX Notu

Şifre güvenliği düzeltildi:
- `Users.csv` içindeki `PasswordPlain` düz yazı şifre alanı kaldırıldı.
- Kullanıcı şifreleri yalnızca PBKDF2-SHA256 hash + salt olarak saklanır.
- Eski sürümden gelen `PasswordPlain` alanı ilk açılışta otomatik temizlenir.
- Kullanıcı Yönetimi ekranındaki şifre gösterme kolonu kaldırıldı.
- Şifre CSV içinde düz yazı veya geri çözülebilir şifreli alan olarak tutulmaz; yalnızca hash + salt saklanır.
- Yeni kullanıcı oluştururken şifre zorunludur; mevcut kullanıcıda şifre alanı boş bırakılırsa eski şifre korunur.

Not: İlk kurulum kullanıcısı `admin` olarak kalır; parolası rastgele üretilir ve ilk girişte değiştirilmesi zorunludur.

## SECURITY + CSV + SQL Altyapı Notu

Bu sürümde kullanıcı yönetimi, CSV dayanıklılığı ve SQL Server hazırlık katmanı güncellendi:

- Kullanıcı Yönetimi ekranındaki `Şifre` kolonu kaldırıldı; admin şifreleri görüntüleyemez.
- `PasswordProtected` geri çözülebilir şifre alanı artık kullanılmaz ve CSV yeniden yazılırken başlıktan düşürülür.
- `PasswordPlain` alanı kullanılmaz; eski CSV dosyasında varsa ilk açılışta hash + salt alanlarına taşınır ve CSV başlığından düşürülür.
- Giriş doğrulaması yine PBKDF2-SHA256 `PasswordHash` + `PasswordSalt` üzerinden yapılır.
- CSV yazma işlemleri güçlendirildi: kilitli dosyada tekrar deneme, atomik yazma, `.tmp` dosya üzerinden güvenli değiştirme ve `.bak` yedek oluşturma eklendi.
- SQL Server altyapısı için `Sql/Schema.sql`, `Sql/DatabaseConfig.sample.ini`, `Services/DatabaseConfig.vb` ve `Services/SqlDatabaseService.vb` eklendi.
- Projeye `Microsoft.Data.SqlClient` paketi eklendi.
- Program açılışında `Data/Database.config` içinde `Mode=SQL` görülürse SQL şeması otomatik kontrol edilir/oluşturulur.
- `SqlDatabaseService.ImportAllCsvToSqlFromConfig()` metodu mevcut CSV kayıtlarını SQL tablolarına aktarmak için hazırlandı.

Önemli not: Bu sürümde mevcut ekranların tamamı hâlâ CSV üzerinden çalışır. SQL altyapısı kuruldu; tam SQL çalışma modu için bir sonraki aşamada `DataService` metotlarının SQL provider üzerinden ayrılması gerekir.

## DWG/DXF'den Kontrol Ölçüsü Aktarımı

`Kontrol Ölçüleri` ekranındaki `DWG'den Ölçüleri Al` düğmesi, tam AutoCAD kurulumuyla gelen `accoreconsole.exe` aracını kullanır.

- DWG veya DXF içindeki gerçek `DIMENSION` nesneleri taranır.
- Nominal değer, alt/üst tolerans, birim, katman, layout ve ölçü yazısı aday olarak alınır.
- Ölçü yazısı konumları çizim sınırlarına göre yaklaşık PDF X/Y yüzdesine dönüştürülür.
- Adaylar önizleme ekranında seçilebilir, çıkarılabilir ve değerleri düzeltilebilir.
- Benzer nominal ve konumda mevcut ölçüler varsayılan olarak işaretsiz gelir.
- Seçilen ölçüler tek CSV kilidi altında toplu kaydedilir.
- AutoCAD olmayan bilgisayarda ana program normal çalışır; yalnızca DWG/DXF aktarımı kullanılamaz.

Not: Patlatılmış çizgi/metinlerden oluşan ölçüler `DIMENSION` nesnesi olmadığı için otomatik okunmaz. X/Y ve layout-sayfa eşleşmeleri aktarım sonrasında PDF üzerinde kontrol edilmelidir. AutoCAD LT sürümünde Core Console bulunmayabilir.

## UPDATE PACKAGE Altyapı Notu

Program güncelleme süreci standart hale getirildi:

- `build_release.bat` normal Release EXE çıktısını oluşturur.
- `build_release_update_zip.bat`, `build_release_update_zip.ps1` dosyasını çağırır; tek işlemde Release publish alır ve imzalı güncelleme ZIP paketi oluşturur.
- Güncelleme paketleri `UpdatePackages` klasörüne yazılır.
- Paket içine `_update_manifest.txt` eklenir.
- Aynı paketin `TeknikResimOlcum_Update_LATEST.zip` kopyası oluşturulur.
- ZIP için `.sha256.txt` kontrol dosyası üretilir.
- Program Güncelleme Sihirbazı artık yalnızca bu altyapıdan üretilen paketleri kabul eder.
- Kaynak kod ZIP'i, `Forms/Services/Models` içeren paketler veya manifest içermeyen ZIP dosyaları reddedilir.
- Güncelleme uygulanırken önce mevcut program dosyaları yedeklenir; sonra eski program dosyaları temizlenip yeni yayın dosyaları kopyalanır.
- `Data`, `Drawings`, `Temp`, `Backups` ve `Updates` klasörleri korunur.

Kullanım:

1. Geliştirici bilgisayarında `build_release.bat` çalıştırılır.
2. `UpdatePackages` klasöründe oluşan ZIP dosyası alınır.
3. Çalışan programda Admin > Program Güncelleme Sihirbazı açılır.
4. Bu ZIP dosyası seçilir ve güncelleme başlatılır.

## MAIN UI RESPONSIVE FIX Notu

Ana ekran yerleşimi geniş ekran kullanımı için düzenlendi:

- Üstteki ticket özet kartları sabit genişlik yerine ekran genişliğine göre eşit dağıtılır.
- Ana menü panelleri FlowLayoutPanel yerine TableLayoutPanel düzenine alındı.
- Menü grupları pencere genişliğine göre 1, 2 veya 3 sütunlu yerleşir.
- Grup panelleri artık sabit genişlikte kalmaz; bulunduğu hücreyi doldurur.
- Buton genişlikleri panel genişliğine göre otomatik ayarlanır.
- Uzun buton yazılarının kırpılması azaltıldı; buton yüksekliği artırıldı.
- Geniş monitörde sol üstte sıkışan görünüm yerine ekranı daha dengeli kullanan yapı oluşturuldu.
- Güncelleme altyapısı dosyaları korunarak devam ettirildi.

## MAIN UI HEADER OVERLAP FIX Notu

Ana ekran grup başlıklarının butonların üstüne binmesi düzeltildi:

- Grup başlık bandı ve buton alanı aynı panel içinde serbest Dock düzeninden çıkarıldı.
- Her grup kartının içi iki satırlı TableLayoutPanel yapısına alındı.
- 1. satır sabit 42 px başlık alanı, 2. satır buton alanı olarak ayrıldı.
- Böylece başlık yazıları ile butonlar üst üste gelmez.
- Önceki geniş ekran düzeni korunmuştur.

## URETIM ETIKET ROLU Notu

`Uretim Etiket` rolu eklendi:

- Kullanici Yonetimi ekraninda rol olarak secilebilir.
- Ana ekranda yalnizca `Baglanacak Kalip Listesi` ekranini gorur.
- Kalip baglama, ticket, dashboard, kalite ve yonetim ekranlarina erisemez.
- `Baglanacak Kalip Listesi` ekraninda sadece listeyi gorur; `Excel'den Al` aktarim islemi gosterilmez ve yetki kontroluyle engellenir.

## PLASTIKHANE VARDIYA TAKIP LISTESI Notu

`Plastikhane Kalite Kontrol` rolu ve vardiya takip ekrani eklendi:

- Tarih/saat, hatali adet, sorumlu, urun adi-kodu, sorun ve alinan aksiyon kaydedilir.
- Sari Kart, Kalip Tadilat, Hata Raporu ve Test secenekleri ayri olarak izlenir.
- `Kalip Tadilat` secildiginde Kalip Ticket bilgileri acilir; vardiya kaydi kaydedilirken ticket kayda baglanir ve ayni kayittan ikinci ticket olusmasi engellenir.
- `Plastikhane Kalite Kontrol` ve `Admin` kayit ekleyebilir ve duzenleyebilir.
- Kayit silme yalnizca `Kalite Kontrol Yoneticisi` ve `Admin` rollerindedir.
- Yeni kaydin tarih/saat bilgisi sistem tarafindan otomatik atanir ve sonradan degistirilemez.
- `Uretim Kullanicisi`, `Uretim Yoneticisi`, `Mekanizma Yoneticisi` ve `Mekanizma Kalite Kontrol` rolleri ekrani salt okunur kullanir.
- Kayit olusturma/guncelleme kullanicisi ve zaman bilgisi denetim amaciyla saklanir.
- Veriler `Data\\PlasticShiftTrackingRecords.csv` dosyasinda eszamanli erisim kilidiyle tutulur.

## YETKI MATRISI PENCERESI Notu

- Admin ana ekranindaki `Yonetim ve Sistem` kartina `Yetki Matrisi` dugmesi eklendi.
- Roller satirlarda, ekran ve islevler sutunlarda gosterilir.
- Tam, sinirli, salt okunur ve erisim yok durumlari renklerle ayrilir.
- Rol, yetki tipi ve serbest metin filtreleri bulunur.
- Pencere salt okunurdur; kaynak olarak guncelleme paketindeki `Docs\\YETKI_MATRISI.csv` dosyasini kullanir.
- Ayrintili Markdown belgesi ve CSV dosyasi pencereden acilabilir.

## GIZLI YETKI TEST HESAPLARI Notu

- Kullanici Yonetimi ekranina `Giris ekraninda goster` ve `Yetki test hesabi` secenekleri eklendi.
- Yetki test hesabi secildiginde kullanici giris listesinden otomatik gizlenir.
- Gizli hesaplar giris ekranindaki `Yonetici / Gizli Test Hesabi` alanina kullanici adi manuel yazilarak acilir.
- Test hesabi yeni bir rol olusturmaz; secilen mevcut rolun gercek yetkilerini kullanir.
- Test hesabiyla giris yapildiginda ana pencere basligi ve aktif kullanici alani sari uyariyla isaretlenir.
- Test hesabi islemleri kendi kullanici adiyla loglanir.

## OLCUM KAYDI YAKINLASTIRMA Notu

- Olcum Kaydi / Teknik Resim Inceleme ekraninin Fit ve zoom sistemi Olcum Girisi ile ayni hale getirildi.
- Olcum balonuna veya sagdaki olcum satirina tiklaninca ilgili teknik resim bolgesi buyutulup ortalanir.
- Secili olcu `+ / -` ile yakinlastirilirken ekranin merkezinde korunur.
- `Ctrl + fare tekerlegi` ile imlecin bulundugu noktaya yakinlastirma ve uzaklastirma yapilabilir.
- Balonlar resimle ayni koordinat sisteminde kalirken sabit ekran boyutunda tutulur; resim buyudukce balonun kapladigi goreli alan kuculur ve teknik resim olcusu okunabilir hale gelir.

## URETIM TICKETLARI GRUPLU LISTE Notu

- Uretim Ticketlari listesi Olcum Gecmisi mantiginda TR Kodu bazinda gruplandirildi.
- Ilk gorunumde her TR icin ticket, durum, olcum kaydi, goz ve NOK sayilarini gosteren tek bir ozet satiri bulunur.
- Ok simgesine veya grup satirina cift tiklaninca o TR'ye ait ticketlar tarih sirasiyla acilir.
- Goruldu, Ticketi Kapat ve Olcum Kaydini Ac islemleri yalnizca grup icindeki gercek ticket satirinda calisir.
- Arama ve durum filtreleri gruplu gorunumle birlikte calismaya devam eder.

## PLASTIKHANE VARDIYA E-POSTA RAPORU Notu

- Plastikhane Vardiya Takip Listesi arac cubuguna E-posta Raporu dugmesi eklendi.
- Ekranda o anda uygulanan arama ve tarih filtresindeki kayitlar rapora aktarilir.
- E-posta govdesinde kayit/hata toplamlarinin ozeti ile ayrintili vardiya tablosu bulunur.
- Sari Kart, Kalip Tadilat, Hata Raporu ve Test bilgileri raporda ayri sutunlar halinde gosterilir.
- Outlook icinde duzenlenebilir taslak acilir; alici, bilgi ve metin kullanici tarafindan tamamlanir.
- E-posta otomatik gonderilmez.

## MEKANIZMA KALITE E-POSTA RAPORU Notu

- Mekanizma Kalite Kontrol Formu arac cubuguna `E-posta Raporu` dugmesi eklendi.
- Rapor, ekranda o anda uygulanan durum ve arama filtresinden sonra gorunen kayitlari kullanir; veri kaynagi yeniden okunmaz.
- E-posta govdesinde kayit, gelen goz, bekleyen, uygun/uygun degil ve bugun teslim/kontrol ozetleri ile ayrintili kontrol tablosu bulunur.
- Outlook'ta alici, konu ve govdesi degistirilebilen normal bir taslak acilir.
- E-posta otomatik gonderilmez.

## MEKANIZMA KONTROL TESLIM ZAMANI Notu

- Mekanizma Kalite Kontrol kayit listesinde teslim tarihi/saati ve kontrol tarihi/saati ayri sutunlarda gosterilir.
- Kayit detayindaki Teslim Ozeti kartinda teslim tarihi ve saati acikca yer alir.

## PLASTIKHANE VARDIYA HATALI MIKTAR Notu

- Vardiya takip kaydindaki `Hatali Adet / Miktar` alani serbest metin olarak calisir.
- `1 Adet`, `1 Koli`, `1 Kutu`, `1 Palet` gibi degerler aynen kaydedilir ve liste/raporda gosterilir.
- Eski sayisal kayitlar degistirilmeden goruntulenmeye devam eder.

## MEKANIZMA KONTROL GUN FILTRESI Notu

- Mekanizma Kalite Kontrol Formuna teslim tarihini esas alan istege bagli `Teslim Gunu` filtresi eklendi.
- Filtre isaretliyken yalnizca secilen gun teslim edilen kayitlar, isaret kaldirildiginda tum gunler gosterilir.
- E-posta raporu da ekranda secili gun filtresini aynen kullanir.

## BUGUN BAGLANACAK KALIPLAR EKSIK TEKNIK RESIM Notu

- Urun / Teknik Resim Yonetimi ekranina `Bugunun Eksik Resimleri` dugmesi eklendi.
- Guncel Baglanacak Kalip Listesindeki 1. ve 2. baglanacak TR kodlari tekillestirilerek kontrol edilir.
- Pencerede teknik resmi bulunan ve eksik olan TR'ler birlikte gorulebilir; varsayilan gorunum yalnizca eksikleri gosterir.
- Ayni TR birden fazla makine veya kalipta kullaniliyorsa tek satirda birlestirilir.
- Eksik satira cift tiklanarak ilgili urun kaydi Teknik Resim Yonetimi ekraninda acilabilir.

## YONETICI ROLU SALT OKUNUR YETKI DUZENI Notu

- Genel `Yonetici` rolu operasyonel kayit olusturan veya kapatan bir rol olmaktan cikarildi.
- Olcum Gecmisi, Kalite/Plastikhane/Mekanizma, INO, baglanacak kalip ve dashboard, Uretim/Kalip ticketlari, teknik resim ve kontrol olculeri genis salt-okunur gorunurlukle acilir.
- Yonetici yeni olcum giremez; ticketi goruldu yapamaz, olusturamaz veya kapatamaz; kalip baglama baslatamaz ya da tamamlayamaz; plan aktaramaz.
- INO penceresi Yonetici icin zorunlu salt-okunur modda calisir.
- Urun / Teknik Resim ve Kontrol Olculeri pencerelerinde duzenleme araclari gizlenir; teknik resim ve eksik resim durumu goruntulenebilir.
- Yonetici Yetki Matrisini gorur; Kullanici Yonetimi, loglar ve Program Guncelleme Sihirbazina erisemez.
- Yazma islemleri yalnizca arayuzde degil servis katmaninda da yetki kontrolunden gecirilir.

## OLCUM GIRISI DUSUK COZUNURLUK VE DPI Notu

- Olcum Girisi penceresindeki PDF arac cubugu dar ekranlarda yatay kaydirma kullanmak yerine iki satira sarilir.
- Dusuk mantiksal ekran genisliginde teknik resim/olcum bolme orani otomatik degisir ve olcum bilgi paneline daha fazla alan ayrilir.
- Yerlesim ekran yeniden boyutlandirildiginda veya Windows DPI olcegi degistiginde yeniden hesaplanir.

## MEKANIZMA KONTROL FILTRE OZETI Notu

- Filtre satirinin sagindaki gunluk ozet, farkli DPI degerlerinde kesilmemesi icin `Bugun teslim` ve `Bugun kontrol` seklinde kisaltildi.

## OLCUM GECMISI GUNLUK SAYAC VE CSV GIZLILIGI Notu

- Olcum Gecmisi ozetinde bugun olusturulan benzersiz olcum kaydi sayisi gosterilir; ayni kaydin olcu satirlari sayiyi sisirmez.
- Gunluk sayac mevcut arama ve sonuc filtrelerinden bagimsizdir.
- CSV dosya konumlari, kaynak yolu metinleri ve `CSV Klasoru` dugmeleri Admin dahil hicbir kullaniciya gosterilmez.

## INO SUTUN BAZLI FILTRELEME Notu

- INO-1 / INO-2 Takip Formuna tum veri sutunlarini kapsayan sutun secimi ve sutuna ozel arama alani eklendi.
- Farkli sutunlara girilen filtreler birlikte calisir; genel arama ve durum filtresiyle de ayni anda uygulanabilir.
- Aktif sutun filtresi sayisi ekranda gosterilir ve filtrelenen sutun basliklari renkli olarak vurgulanir.
- Sutun filtreleri ayri olarak veya tum filtrelerle birlikte tek dugmeyle temizlenebilir.

## MEKANIZMA YONETICISI VE YONETICI YETKI DUZENI Notu

- `Mekanizma Yoneticisi`, Mekanizma Kontrol Formundaki kayit listesini ve ayrintilari salt okunur gorur; yeni teslim olusturamaz veya bekleyen teslimi sonuclandiramaz.
- Sonuclandirma dugmeleri arayuzde gizlenir; dogrudan form ya da servis cagrisi da yetki katmaninda engellenir.
- `Yonetici`, Kullanici Yonetimi, Log Kayitlari ve Program Guncelleme Sihirbazina erisemez.
- Yoneticinin Yetki Matrisi goruntuleme yetkisi bu uc sistem ekranindan ayri olarak korunur.

## GIRIS EKRANI DINAMIK PENCERE VE DPI Notu

- Giris penceresi acildigi monitorun gercek calisma alanina ve Windows DPI olcegine gore yeniden boyutlandirilip ortalanir.
- Dar veya dikey alani az ekranlarda dis bosluklar, baslik, iki kolon orani ve form satirlari kompakt profile gecer.
- Pencere ekrandan tasmasin diye minimum ve hedef boyutlar monitor sinirlari icinde hesaplanir; form icinde yatay kaydirma kullanilmaz.
- Pencere farkli DPI degerine sahip bir monitore tasindiginda, Windows olceklemesi tamamlandiktan sonra yerlesim ikinci kez dogrulanir.

## OLCUM GECMISI DINAMIK YERLESIM Notu

- Olcum Gecmisi filtre araclari pencere genisligine gore satirlara ayrilir; sabit yukseklik nedeniyle dugme veya alanlar birbirinin arkasinda kalmaz.
- Filtre bolumunun yuksekligi gercek icerige gore hesaplanir; cok kucuk ekranlarda tablo icin minimum alan korunarak bolum kaydirilabilir olur.
- Gunluk sayac ve grup ozeti dar ekranda birden fazla satira gecebilir ve ozet satiri metnin yuksekligine gore buyur.
- Uzun tablo basliklari otomatik sarilir ve baslik satiri metni kirpmayacak sekilde yukselir.
- Yerlesim pencere boyutu veya monitor DPI degeri degistiginde yeniden hesaplanir.

## OLCUM GECMISI OLCU ADI SUTUNU Notu

- `Olcu Adi`, grup acma/kapatma alanindan sonraki ilk veri sutunu olarak sola tasindi.
- Sutun genisligi baslik ve gorunen kayit iceriklerine gore otomatik hesaplanir.
- Cok uzun olcu adlarinin diger sutunlari tamamen kapatmamasini saglayan ekran genisligi ve DPI uyumlu ust sinir uygulanir.

## OLCUM KAYDI TEKNIK RESIM BASLANGIC FIT Notu

- Olcum Kaydi inceleme penceresinde teknik resim ilk acilista goruntuleme alanina Fit edilir.
- Gecmisten belirli bir olcu satiriyla acildiginda ilgili balon secili kalir ancak otomatik yakinlastirma yapilmaz.
- Kullanici daha sonra balona veya olcu satirina tikladiginda balona odaklanma ve yakinlastirma davranisi devam eder.
- Web goruntusu ve pencere yerlesimi tamamlandiktan sonra Fit hesabi kisa bir ikinci gecisle dogrulanir.

## OLCUM KAYDI OLCUMU YAPAN BILGISI Notu

- Olcum Kaydi inceleme penceresinin ust bilgi alaninda kayit numarasinin yaninda `Olcumu Yapan` kullanici adi gosterilir.
- Bilgi hem normal olcum hem de goz kapali kayitlarindan okunur; kullanici bilgisi bulunmayan eski kayitlarda `-` gosterilir.

## INO DAR PENCERE DUGME YERLESIMI Notu

- INO-1 / INO-2 Takip Formundaki ust islem dugmeleri, sutun filtresi ve durum dugmeleri pencere daraldiginda alt satira gecebilir.
- Komut ve filtre bolumlerinin yuksekligi gorunen dugme sayisi ile pencere genisligine gore otomatik hesaplanir.
- Sag taraftaki dugmeler yatay gorunur alanin disinda kalmaz; fiziksel yukseklik yetersizse ilgili satir kendi icinde kaydirilabilir olur.
- Kullanici rolu degisip dugmeler gizlendiginde veya gorundugunde yerlesim yeniden hesaplanir.
- Pencere boyutu ve monitor DPI degeri degistiginde yeni satir dagilimi otomatik uygulanir.

## MEKANIZMA KONTROL FILTRE SATIRI DPI Notu

- Mekanizma Kontrol Formundaki filtre satiri, pencere yuksekligi azaldiginda sabit bir degerle gereksiz yere daraltilmaz.
- Arama, durum ve teslim gunu kontrollerinin tercih edilen yuksekligi ile panel bosluklari olculerek satirin alt siniri belirlenir.
- DPI olcegi arttiginda guvenli minimum satir yuksekligi ayni oranda artar; kontroller alttan kirpilmaz.
- Pencere farkli DPI degerindeki bir monitore tasindiginda yukseklik hesaplamasi tamamlanan olceklemeden sonra yenilenir.

## URUN TEKNIK RESIM YONETIMI RESPONSIVE YERLESIM Notu

- Urun / Teknik Resim Yonetimi penceresindeki sabit Left/Top koordinatlari tablo ve akis yerlesimiyle degistirildi.
- TR, revizyon, urun ve sifreli dosya alanlari pencere genisligini kontrollu olarak paylasir.
- Islem dugmeleri dar pencerede alt satira gecer; `Bugunun Eksik Resimleri` ve diger dugmeler sag tarafta kaybolmaz.
- Liste sayaci, arama, durum ve `Filtreyi Temizle` kontrolleri tek responsive satirda mevcut genisligi paylasir.
- Ust form ve liste filtre bolumlerinin yuksekligi icerik, pencere boyutu ve DPI degerine gore yeniden hesaplanir.
- Eski zorunlu yatay kaydirma genislikleri kaldirildi.

## KALIP BAGLAMA TAKIBI RESPONSIVE YERLESIM Notu

- Urun bilgisi, yeni baglama formu, devam eden baglamalar listesi ve alt islem satiri pencerenin kullanilabilir yuksekligini dinamik olarak paylasir.
- Alt islem dugmeleri her zaman gorunur alanda tutulur; pencere daraldiginda dugmeler kirpilmak yerine yeni satira gecer.
- Urun bilgi bolumundeki gereksiz dikey bosluk azaltildi ve devam eden baglamalar listesine kullanilabilir alan birakildi.
- Pencere boyutu veya Windows DPI degeri degistiginde satir yukseklikleri ve dugme alani yeniden hesaplanir.

## MEKANIZMA KONTROL URUN ADI TAMAMLAMA Notu

- Baglanacak kalip planindaki TR icin secilen aktif/son revizyonda urun adi bos olsa bile ayni TR'nin diger kayitlarindaki ad kullanilir.
- TR bicim farklari, basta bulunan sifirlar ve Excel'den gelebilen sayisal `.0` son eki normalize edilerek eslestirilir.
- TR ile ad bulunamazsa P kodu ve kalip kodu yalnizca tek bir urun adina isaret ediyorsa guvenli geri donus olarak kullanilir.
- Daha once kaydedilmis bos urun adlari liste, detay ve e-posta raporu hazirlanirken tamamlanmaya calisilir; veri bulunamazsa urun adi alani bos birakilir.
- Urun adi cozulemeyen Calisan, 1. Baglanacak ve 2. Baglanacak TR'ler `Bugunun Eksik Resimleri` denetiminde eksik kayit olarak gosterilir.

## EKSIK URUN BILGILERI E-POSTA RAPORU Notu

- Eksik Urun Bilgileri penceresine `E-posta Hazirla` dugmesi eklendi.
- Outlook taslagi yalnizca ekranda filtrelenmis kayitlari icerir; otomatik olarak gonderilmez.
- Eksik hucreler HTML tabloda kirmizi vurgulanir; urun adi ve kontrol olcusu eksik sayilari raporun ust ozetinde gosterilir.
- Hazirlanan rapor kullanici, rol, filtre ve hazirlanma tarihi bilgilerini icerir.

## KONTROL OLCULERI ISLEM DUGMELERI Notu

- DWG/DXF aktarimi, kaydetme, pasif yapma ve yeni olcu dugmeleri ilk satirin genisligini esit olarak paylasir.
- Sira degistirme, olcu silme ve grup alani islemleri ikinci satirda hizali olarak gosterilir.
- Sabit yatay koordinatlar kaldirildi; islem alani pencere genisligi degistiginde yeniden boyutlanir.
- Dugme metinleri kirpilmaz ve sag taraftaki islemler gorunur alanin disinda kalmaz.

## ANA EKRAN KPI KART YUKSEKLIGI Notu

- Genis ve standart ekranlarda KPI satirinin yuksekligi azaltildi; alt menu gruplarina daha fazla dikey alan birakildi.
- Kart basligi ile sayac arasindaki bosluklar sikilastirildi.
- Dar ekranda KPI kartlari iki satira gectiginde kart basina okunabilir yukseklik korunur.
- KPI yuksekligi pencere profili ve Windows DPI olcegine gore hesaplanmaya devam eder.

## BAGLANACAK KALIPLAR TEKNIK RESIM E-POSTA RAPORU Notu

- Bugun Baglanacak Kaliplar - Teknik Resim Kontrolu penceresine `E-posta Hazirla` dugmesi eklendi.
- Outlook taslagi ekrandaki arama ve `Yalnizca eksik resimler` filtresine uyan satirlardan olusur; otomatik gonderilmez.
- Raporda teknik resim var/eksik ozeti ile TR, urun, revizyon, dosya, plan sirasi, makine, kalip, P kodu ve aciklama bulunur.
- Eksik kayitlar ve eksik hucreler HTML tabloda kirmizi vurgulanir.

## ANA EKRAN EKSIK TEKNIK RESIM UYARISI Notu

- `Urun / Teknik Resim Yonetimi` dugmesi, baglanacak kaliplarda eksik teknik resim veya urun bilgisi varsa kirmizi gosterilir.
- Dugme metninde guncel eksik kayit sayisi yer alir.
- Eksik kayit kalmadiginda dugme otomatik olarak normal metnine ve rengine doner.
- Sayac ana ekran ozetleriyle birlikte arka planda ve pencereye yeniden donuldugunde yenilenir.

## TEST / TALEP YONETIMI Notu

- Ana ekrana `Laboratuvar ve Test Yonetimi` grubu ile `Test / Talep Formu` dugmesi eklendi.
- Talep eden bolum, talep edilen bolum, coklu talep nedeni, urun/TR, istenen test, numune miktari, oncelik, termin, referans rapor no ve aciklama kaydedilir.
- `Giris Kalite Kontrol` rolu eklendi; bu rol Test / Talep Formu uzerinden GKK bolumu adina talep olusturabilir.
- Talep nedeni secenekleri genis ekranda yatay dagilacak, dar ekranda satira saracak sekilde duzenlendi.
- Talep acan kullanici ile tarih/saat sistem tarafindan otomatik atanir; dijital kayitta imza yerine kullanici ve zaman damgasi kullanilir.
- Kalite Laboratuvar, Kalite Kontrol Yoneticisi ve Admin yeni talepleri isleme alabilir; laboratuvar rapor no, sonuc ve aciklama girerek tamamlayabilir.
- Talebi acan kullanici kendi acik talebini; laboratuvar yetkilileri ise yetkili olduklari acik talepleri iptal edebilir.
- Test talepleri `Yeni`, `Islemde`, `Tamamlandi` ve `Iptal` durumlariyla izlenir; termin gecen acik talepler listede vurgulanir.
- Ana ekran dugmesinde yeni ve islemdeki toplam talep sayisi gosterilir; bekleyen is varsa dugme kirmizi olur.
- TestRequestRecords.csv veri dosyasi kilitli/atomik CSV islemleriyle yonetilir ve SQL aktarim semasina dahil edilmistir.
- Yetki matrisi `Test Talepleri` sutunuyla guncellenmistir.
