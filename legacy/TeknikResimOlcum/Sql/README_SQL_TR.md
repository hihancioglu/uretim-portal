# TeknikResimOlcum SQL Altyapısı

Bu klasör SQL Server altyapısı için hazırlanmıştır.

## Kullanım

1. SQL Server Express veya LocalDB üzerinde `TeknikResimOlcum` isminde bir veritabanı oluşturun.
2. `Sql/Schema.sql` dosyasını veritabanında çalıştırın.
3. `Sql/DatabaseConfig.sample.ini` dosyasını programın çalışma klasöründeki `Data/Database.config` konumuna kopyalayın.
4. Dosya içinde `Mode=SQL` yapın.
5. `ConnectionString` alanını kendi SQL Server adresinize göre düzenleyin.

## Not

Bu sürümde CSV dosyaları güçlendirildi ve SQL Server altyapı katmanı oluşturuldu. Mevcut ekranların tamamını SQL üzerinden çalıştırmak için ikinci aşamada `DataService` metotlarının SQL provider üzerinden ayrılması gerekir.

Hazırlanan altyapı dosyaları:

- `Services/DatabaseConfig.vb`
- `Services/SqlDatabaseService.vb`
- `Sql/Schema.sql`
- `Sql/DatabaseConfig.sample.ini`

## CSV'den SQL'e Aktarım

`SqlDatabaseService.ImportAllCsvToSqlFromConfig()` metodu, mevcut CSV dosyalarını SQL tablolarına aktaracak şekilde hazırlanmıştır. Bu metot elle çağrılabilir veya sonraki aşamada admin ekranına bir buton olarak bağlanabilir.
