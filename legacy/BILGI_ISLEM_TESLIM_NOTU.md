# A Blok Kalite Kontrol Süreçleri — Kaynak Kod Teslimi

Bu klasör bilgi işlem birimiyle kaynak kod paylaşımı için hazırlanmıştır.

## Paket kapsamı

- Ana WinForms uygulamasının güncel VB.NET kaynak kodu
- Program başlatıcısının kaynak kodu
- INO entegre modülünün kaynak kodu
- Hurda, REWORK ve ana ekran HTML kaynakları
- Çevrimdışı kullanılan JavaScript kütüphaneleri
- SQL şeması ve örnek yerel yapılandırma
- Yetki matrisi ve teknik dokümantasyon
- Derleme ve yayın yardımcı betikleri

## Bilerek hariç tutulan içerikler

- Kullanıcı kayıtları ve parola özetleri
- Ölçüm, ticket, hurda, REWORK, vardiya ve INO işlem verileri
- E-posta alıcı tanımları
- Oturum, audit ve hata logları
- Ek dosyalar, fotoğraflar, teknik resimler ve yerel taslaklar
- `bin`, `obj`, `publish` ve `UpdatePackages` derleme çıktıları
- Yerel/veritabanı çalışma yapılandırması
- Güncelleme paketlerini imzalayan `UpdateSigningPrivateKey.xml` özel anahtarı

`Resources/INO/INO_Database.seed.csv` dosyası proje yapısının korunması için bulunmaktadır; yalnızca başlık satırını içerir ve gerçek kayıt barındırmaz.

## Geliştirme ortamı

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 veya güncel `dotnet` CLI
- Microsoft Edge WebView2 Runtime
- NuGet erişimi

## Derleme

Ana proje:

```powershell
dotnet restore .\TeknikResimOlcum\TeknikResimOlcum.vbproj
dotnet build .\TeknikResimOlcum\TeknikResimOlcum.vbproj -c Release -r win-x64
```

Başlatıcı:

```powershell
dotnet build .\TeknikResimOlcumLauncher\TeknikResimOlcumLauncher.vbproj -c Release -r win-x64
```

## Yapılandırma notları

- Uygulama varsayılan olarak CSV altyapısını kullanır ve çalışma verilerini kaynak kod klasöründen ayrı konumlarda oluşturur.
- SQL denemesi için `TeknikResimOlcum/Sql/DatabaseConfig.sample.ini` örneği kullanılabilir. Dosyada gerçek sunucu veya kullanıcı bilgisi yoktur.
- İmzalı güncelleme paketi oluşturulacaksa özel imzalama anahtarı bilgi işlem tarafından güvenli anahtar yönetimi prosedürüyle ayrıca sağlanmalıdır. Özel anahtar kaynak kontrolüne eklenmemelidir.
- Canlı sistem verileri gerektiğinde ayrı, şifreli ve yetkilendirilmiş veri aktarım süreci kullanılmalıdır.
- `Services/UserService.vb` içinde eski kurulumlardaki sabit yönetici parolasını tespit edip rastgele geçici parolaya taşıyan geriye dönük uyumluluk kontrolü bulunur. Bu bir canlı kullanıcı kaydı değildir; bilgi işlem güvenlik incelemesinde geçiş tamamlandıktan sonra kaldırılması değerlendirilmelidir.

## Güvenlik kontrolü

Teslim paketi oluşturulurken veri, log, yedek, derleme çıktısı, özel anahtar ve gerçek e-posta adresi taraması yapılmıştır.
