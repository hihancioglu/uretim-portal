# Güncelleme Paketi Oluşturma Altyapısı

Bu klasörde iki build scripti vardır:

- `build_release.bat`
- `build_release_update_zip.bat`
- `build_release_update_zip.ps1`

`build_release.bat`, geriye uyumluluk için bırakılmıştır ve doğrudan `build_release_update_zip.bat` dosyasını çağırır. Batch dosyası sade tutulmuştur; asıl build ve paketleme işlemi PowerShell scripti ile yapılır.

## Ne yapar?

`build_release_update_zip.bat` çalıştırıldığında arka planda `build_release_update_zip.ps1` dosyasını çağırır. Ana işlem PowerShell dosyasında yapılır.

1. `.NET 8` ile Release publish alır.
2. Yayın klasörünü oluşturur.
3. Yayın klasörüne `_update_manifest.txt` dosyasını ekler.
4. `UpdatePackages` klasörüne güncelleme ZIP paketi üretir.
5. Aynı paketin `TeknikResimOlcum_Update_LATEST.zip` kopyasını oluşturur.
6. ZIP için `.sha256.txt` kontrol dosyası üretir.

## Oluşan paket nerede?

Paket şu klasörde oluşur:

`UpdatePackages`

Örnek dosya adı:

`TeknikResimOlcum_Update_20260616_20260616_153000.zip`

## Program nasıl güncellenir?

1. Programı Admin olarak açın.
2. `Program Güncelleme Sihirbazı` ekranına girin.
3. `UpdatePackages` klasöründe oluşan ZIP dosyasını seçin.
4. Güncellemeyi başlatın.

## Önemli

Program Güncelleme Sihirbazı artık kaynak kod ZIP paketlerini kabul etmez.
Güncelleme için bu scriptin oluşturduğu ve içinde `_update_manifest.txt` bulunan yayın ZIP paketi seçilmelidir.

Korunan klasörler:

- `Data`
- `Drawings`
- `Temp`
- `Backups`
- `Updates`

Bu klasörler güncelleme sırasında silinmez veya üzerine yazılmaz.

Teknik resim şifreleme anahtarı `Data\DrawingEncryption.key` dosyasında tutulur.
Bu anahtar her ortak kurulum için rastgele oluşturulur, EXE içinde bulunmaz ve
güncelleme sırasında `Data` klasörüyle birlikte korunur. Program birden fazla
bilgisayardan ortak klasör üzerinden kullanılacaksa `Data` ve `Drawings`
klasörlerinin paylaşım/NTFS izinleri yalnızca yetkili şirket kullanıcılarına
verilmelidir. Anahtar dosyası kaybolursa TROP2 teknik resimler açılamayacağı için
kurumsal yedekleme kapsamına alınmalıdır.

Eski TROP1 teknik resimler güncelleme paketindeki tek kullanımlık geçiş anahtarıyla
TROP2/AES-GCM biçimine dönüştürülür. Tüm eski çizimler dönüştürüldüğünde geçiş
anahtarı uygulama tarafından silinir.

## Güvenli kurulum ve otomatik geri dönüş

Güncelleme uygulanmadan önce paket ayrı bir geçici klasöre çıkarılır ve gerekli
uygulama, manifest ve imza dosyaları kontrol edilir. Mevcut program dosyalarının
yedeği oluşturulup dosya bütünlüğü doğrulanmadan canlı kurulum değiştirilmez.

Kurulum başladıktan sonra silme, kopyalama veya doğrulama adımlarından biri
başarısız olursa önceki program sürümü `Backups` klasöründeki yedekten otomatik
olarak geri yüklenir ve uygulama eski sürümle yeniden başlatılır.
