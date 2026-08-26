Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading

Public NotInheritable Class CryptoService
    Private Sub New()
    End Sub

    Private Shared ReadOnly MagicV1 As Byte() = Encoding.ASCII.GetBytes("TROP1")
    Private Shared ReadOnly MagicV2 As Byte() = Encoding.ASCII.GetBytes("TROP2")
    Private Shared ReadOnly KeyCacheLock As New Object()

    Private Const KeySize As Integer = 32
    Private Const KeyIdSize As Integer = 16
    Private Const NonceSize As Integer = 12
    Private Const TagSize As Integer = 16
    Private Const RetryCount As Integer = 100
    Private Const RetryDelayMs As Integer = 100

    Private Shared cachedKey As DrawingKeyMaterial

    Private NotInheritable Class DrawingKeyMaterial
        Public Property KeyId As Guid
        Public Property KeyBytes As Byte() = Array.Empty(Of Byte)()
    End Class

    Private NotInheritable Class LegacyKeyMaterial
        Public Property EncryptionKey As Byte() = Array.Empty(Of Byte)()
        Public Property MacKey As Byte() = Array.Empty(Of Byte)()
    End Class

    Public Shared Sub EnsureKeyStore()
        Dim ignored = CurrentKey()
    End Sub

    Public Shared Sub EncryptDrawing(inputDrawing As String, outputEnc As String)
        If String.IsNullOrWhiteSpace(inputDrawing) Then Throw New ArgumentException("Şifrelenecek teknik resim yolu boş olamaz.", NameOf(inputDrawing))
        If String.IsNullOrWhiteSpace(outputEnc) Then Throw New ArgumentException("Şifreli çıktı yolu boş olamaz.", NameOf(outputEnc))

        Dim plain = ReadAllBytesShared(inputDrawing)
        EncryptV2BytesToFile(plain, outputEnc)
    End Sub

    Public Shared Sub EncryptPdf(inputPdf As String, outputEnc As String)
        EncryptDrawing(inputPdf, outputEnc)
    End Sub

    Public Shared Sub DecryptDrawing(inputEnc As String, outputDrawing As String)
        If String.IsNullOrWhiteSpace(inputEnc) Then Throw New ArgumentException("Şifreli teknik resim yolu boş olamaz.", NameOf(inputEnc))
        If String.IsNullOrWhiteSpace(outputDrawing) Then Throw New ArgumentException("Teknik resim çıktı yolu boş olamaz.", NameOf(outputDrawing))

        Dim encryptedBytes = ReadAllBytesShared(inputEnc)
        Dim isLegacy = StartsWithMagic(encryptedBytes, MagicV1)
        Dim plain As Byte()

        If StartsWithMagic(encryptedBytes, MagicV2) Then
            plain = DecryptV2Bytes(encryptedBytes)
        ElseIf isLegacy Then
            plain = DecryptLegacyBytes(encryptedBytes)
        Else
            Throw New InvalidDataException("Şifreli teknik resim imzası geçersiz.")
        End If

        WriteAllBytesAtomic(outputDrawing, plain)

        If isLegacy Then
            Try
                MigrateLegacyFile(inputEnc)
                DeleteLegacyMigrationKeyIfComplete()
            Catch ex As Exception
                AppendMigrationLog("Erişim sırasında eski teknik resim dönüştürülemedi: " & inputEnc & " | " & ex.Message)
            End Try
        End If
    End Sub

    Public Shared Sub DecryptPdf(inputEnc As String, outputPdf As String)
        DecryptDrawing(inputEnc, outputPdf)
    End Sub

    Public Shared Function MigrateLegacyDrawings() As Integer
        EnsureKeyStore()
        If Not Directory.Exists(AppPaths.DrawingsDir) Then
            DeleteLegacyMigrationKeyIfComplete()
            Return 0
        End If

        Dim migratedCount As Integer = 0
        Dim failedCount As Integer = 0

        For Each drawingPath In Directory.EnumerateFiles(AppPaths.DrawingsDir, "*.enc", SearchOption.AllDirectories)
            Try
                If MigrateLegacyFile(drawingPath) Then migratedCount += 1
            Catch ex As Exception
                failedCount += 1
                AppendMigrationLog("Eski teknik resim dönüştürülemedi: " & drawingPath & " | " & ex.Message)
            End Try
        Next

        If failedCount = 0 Then DeleteLegacyMigrationKeyIfComplete()
        Return migratedCount
    End Function

    Private Shared Function CurrentKey() As DrawingKeyMaterial
        SyncLock KeyCacheLock
            If cachedKey Is Nothing Then cachedKey = LoadOrCreateKey()
            Return cachedKey
        End SyncLock
    End Function

    Private Shared Function LoadOrCreateKey() As DrawingKeyMaterial
        Directory.CreateDirectory(AppPaths.DataDir)

        Using keyLock = AcquireExclusiveLock(AppPaths.DrawingEncryptionKeyFile & ".lock")
            If File.Exists(AppPaths.DrawingEncryptionKeyFile) Then
                Return ReadKeyFile(AppPaths.DrawingEncryptionKeyFile)
            End If

            Dim drawingFormats = InspectEncryptedDrawingFormats()
            If drawingFormats.Any(Function(format) format = 2) Then
                Throw New CryptographicException(
                    "Teknik resim şifreleme anahtarı bulunamadı." & Environment.NewLine &
                    "Data\DrawingEncryption.key dosyasını yedekten geri yükleyiniz. Yeni anahtar oluşturulmadı.")
            End If
            If drawingFormats.Any(Function(format) format <= 0) Then
                Throw New CryptographicException(
                    "Bazı şifreli teknik resimlerin formatı okunamadığı için güvenli biçimde yeni anahtar oluşturulamadı." & Environment.NewLine &
                    "Dosya erişimlerini ve Data\DrawingEncryption.key yedeğini kontrol ediniz.")
            End If

            Dim keyBytes(KeySize - 1) As Byte
            RandomNumberGenerator.Fill(keyBytes)
            Dim material As New DrawingKeyMaterial With {
                .KeyId = Guid.NewGuid(),
                .KeyBytes = keyBytes
            }

            WriteKeyFileAtomic(AppPaths.DrawingEncryptionKeyFile, material)
            Return ReadKeyFile(AppPaths.DrawingEncryptionKeyFile)
        End Using
    End Function

    Private Shared Function ReadKeyFile(keyPath As String) As DrawingKeyMaterial
        Dim settings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        For Each rawLine In File.ReadAllLines(keyPath, Encoding.UTF8)
            Dim line = If(rawLine, "").Trim()
            If line = "" OrElse line.StartsWith("#") Then Continue For
            Dim separator = line.IndexOf("="c)
            If separator <= 0 Then Continue For
            settings(line.Substring(0, separator).Trim()) = line.Substring(separator + 1).Trim()
        Next

        If Not settings.ContainsKey("PackageType") OrElse
           Not String.Equals(settings("PackageType"), "TeknikResimOlcumDrawingKey", StringComparison.Ordinal) Then
            Throw New CryptographicException("Teknik resim anahtar dosyası geçersiz.")
        End If

        Dim keyId As Guid
        If Not settings.ContainsKey("KeyId") OrElse Not Guid.TryParseExact(settings("KeyId"), "N", keyId) Then
            Throw New CryptographicException("Teknik resim anahtar kimliği geçersiz.")
        End If

        Dim keyBytes As Byte()
        Try
            keyBytes = Convert.FromBase64String(If(settings.ContainsKey("KeyBase64"), settings("KeyBase64"), ""))
        Catch ex As FormatException
            Throw New CryptographicException("Teknik resim anahtarı okunamadı.", ex)
        End Try

        If keyBytes.Length <> KeySize Then
            Throw New CryptographicException("Teknik resim anahtar uzunluğu geçersiz.")
        End If

        Dim expectedHash = Convert.ToHexString(SHA256.HashData(keyBytes))
        If Not settings.ContainsKey("KeySha256") OrElse
           Not String.Equals(settings("KeySha256"), expectedHash, StringComparison.OrdinalIgnoreCase) Then
            Throw New CryptographicException("Teknik resim anahtar dosyasının bütünlük kontrolü başarısız.")
        End If

        Return New DrawingKeyMaterial With {.KeyId = keyId, .KeyBytes = keyBytes}
    End Function

    Private Shared Sub WriteKeyFileAtomic(keyPath As String, material As DrawingKeyMaterial)
        Dim text = String.Join(Environment.NewLine, {
            "PackageType=TeknikResimOlcumDrawingKey",
            "Version=2",
            "KeyId=" & material.KeyId.ToString("N"),
            "KeyBase64=" & Convert.ToBase64String(material.KeyBytes),
            "KeySha256=" & Convert.ToHexString(SHA256.HashData(material.KeyBytes)),
            "CreatedAtUtc=" & DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            ""
        })

        Dim tempPath = keyPath & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            File.WriteAllText(tempPath, text, New UTF8Encoding(False))
            File.Move(tempPath, keyPath)
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch cleanupEx As Exception
                ErrorLogService.Log("CryptoService.WriteKeyFile.Cleanup", cleanupEx, "Path=" & tempPath)
            End Try
        End Try
    End Sub

    Private Shared Sub EncryptV2BytesToFile(plain As Byte(), outputEnc As String)
        Dim material = CurrentKey()
        Dim nonce(NonceSize - 1) As Byte
        Dim tag(TagSize - 1) As Byte
        Dim cipher As Byte() = If(plain.Length = 0, Array.Empty(Of Byte)(), New Byte(plain.Length - 1) {})
        RandomNumberGenerator.Fill(nonce)

        Dim keyIdBytes = material.KeyId.ToByteArray()
        Dim associatedData = Combine(MagicV2, keyIdBytes)

        Using aes As New AesGcm(material.KeyBytes, TagSize)
            aes.Encrypt(nonce, plain, cipher, tag, associatedData)
        End Using

        WriteAllBytesAtomic(outputEnc, Combine(MagicV2, keyIdBytes, nonce, tag, cipher))
    End Sub

    Private Shared Function DecryptV2Bytes(allBytes As Byte()) As Byte()
        Dim minimumLength = MagicV2.Length + KeyIdSize + NonceSize + TagSize
        If allBytes.Length < minimumLength Then Throw New InvalidDataException("Şifreli PDF dosyası geçersiz.")

        Dim offset = MagicV2.Length
        Dim keyIdBytes(KeyIdSize - 1) As Byte
        Buffer.BlockCopy(allBytes, offset, keyIdBytes, 0, keyIdBytes.Length)
        offset += keyIdBytes.Length

        Dim fileKeyId As New Guid(keyIdBytes)
        Dim material = CurrentKey()
        If fileKeyId <> material.KeyId Then
            Throw New CryptographicException(
                "Teknik resim farklı bir şifreleme anahtarıyla oluşturulmuş." & Environment.NewLine &
                "Doğru Data\DrawingEncryption.key dosyasını kullanınız.")
        End If

        Dim nonce(NonceSize - 1) As Byte
        Buffer.BlockCopy(allBytes, offset, nonce, 0, nonce.Length)
        offset += nonce.Length

        Dim tag(TagSize - 1) As Byte
        Buffer.BlockCopy(allBytes, offset, tag, 0, tag.Length)
        offset += tag.Length

        Dim cipherLength = allBytes.Length - offset
        Dim cipher As Byte() = If(cipherLength = 0, Array.Empty(Of Byte)(), New Byte(cipherLength - 1) {})
        Dim plain As Byte() = If(cipherLength = 0, Array.Empty(Of Byte)(), New Byte(cipherLength - 1) {})
        If cipherLength > 0 Then Buffer.BlockCopy(allBytes, offset, cipher, 0, cipherLength)

        Try
            Using aes As New AesGcm(material.KeyBytes, TagSize)
                aes.Decrypt(nonce, cipher, tag, plain, Combine(MagicV2, keyIdBytes))
            End Using
        Catch ex As AuthenticationTagMismatchException
            Throw New CryptographicException("Şifreli PDF doğrulaması başarısız. Dosya bozulmuş veya değiştirilmiş olabilir.", ex)
        End Try

        Return plain
    End Function

    Private Shared Function DecryptLegacyBytes(allBytes As Byte()) As Byte()
        If allBytes.Length < MagicV1.Length + 16 + 32 Then Throw New InvalidDataException("Eski şifreli PDF dosyası geçersiz.")

        Dim legacy = ReadLegacyMigrationKey()
        Try
            Dim offset = MagicV1.Length
            Dim iv(15) As Byte
            Buffer.BlockCopy(allBytes, offset, iv, 0, iv.Length)
            offset += iv.Length

            Dim tag(31) As Byte
            Buffer.BlockCopy(allBytes, offset, tag, 0, tag.Length)
            offset += tag.Length

            Dim cipherLength = allBytes.Length - offset
            Dim cipher As Byte() = If(cipherLength = 0, Array.Empty(Of Byte)(), New Byte(cipherLength - 1) {})
            If cipherLength > 0 Then Buffer.BlockCopy(allBytes, offset, cipher, 0, cipherLength)

            Dim expected As Byte()
            Using h As New HMACSHA256(legacy.MacKey)
                expected = h.ComputeHash(Combine(iv, cipher))
            End Using

            If Not CryptographicOperations.FixedTimeEquals(tag, expected) Then
                Throw New CryptographicException("Eski şifreli PDF doğrulaması başarısız.")
            End If

            Using aesAlg As Aes = Aes.Create()
                aesAlg.Key = legacy.EncryptionKey
                aesAlg.IV = iv
                aesAlg.Mode = CipherMode.CBC
                aesAlg.Padding = PaddingMode.PKCS7
                Using decryptor = aesAlg.CreateDecryptor()
                    Return decryptor.TransformFinalBlock(cipher, 0, cipher.Length)
                End Using
            End Using
        Finally
            CryptographicOperations.ZeroMemory(legacy.EncryptionKey)
            CryptographicOperations.ZeroMemory(legacy.MacKey)
        End Try
    End Function

    Private Shared Function ReadLegacyMigrationKey() As LegacyKeyMaterial
        If Not File.Exists(AppPaths.LegacyDrawingMigrationKeyFile) Then
            Throw New CryptographicException(
                "Bu teknik resim eski şifreleme formatında ancak geçiş anahtarı bulunamadı." & Environment.NewLine &
                "İmzalı güncelleme paketini yeniden uygulayınız veya sistem yöneticisine başvurunuz.")
        End If

        Dim settings As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        For Each rawLine In File.ReadAllLines(AppPaths.LegacyDrawingMigrationKeyFile, Encoding.UTF8)
            Dim line = If(rawLine, "").Trim()
            Dim separator = line.IndexOf("="c)
            If separator <= 0 Then Continue For
            settings(line.Substring(0, separator).Trim()) = line.Substring(separator + 1).Trim()
        Next

        Try
            Dim enc = Convert.FromBase64String(If(settings.ContainsKey("EncryptionKeyBase64"), settings("EncryptionKeyBase64"), ""))
            Dim mac = Convert.FromBase64String(If(settings.ContainsKey("MacKeyBase64"), settings("MacKeyBase64"), ""))
            If enc.Length <> 32 OrElse mac.Length <> 32 Then Throw New CryptographicException("Eski teknik resim geçiş anahtarı geçersiz.")
            Return New LegacyKeyMaterial With {.EncryptionKey = enc, .MacKey = mac}
        Catch ex As FormatException
            Throw New CryptographicException("Eski teknik resim geçiş anahtarı okunamadı.", ex)
        End Try
    End Function

    Private Shared Function MigrateLegacyFile(filePath As String) As Boolean
        Dim lockPath = filePath & ".migration.lock"
        Using migrationLock = AcquireExclusiveLock(lockPath)
            Dim encryptedBytes = ReadAllBytesShared(filePath)
            If Not StartsWithMagic(encryptedBytes, MagicV1) Then Return False

            Dim plain = DecryptLegacyBytes(encryptedBytes)
            EncryptV2BytesToFile(plain, filePath)
            Return True
        End Using
    End Function

    Private Shared Sub DeleteLegacyMigrationKeyIfComplete()
        If Not File.Exists(AppPaths.LegacyDrawingMigrationKeyFile) Then Return
        If ContainsLegacyDrawing() Then Return

        Try
            File.Delete(AppPaths.LegacyDrawingMigrationKeyFile)
        Catch ex As Exception
            AppendMigrationLog("Eski geçiş anahtarı silinemedi: " & ex.Message)
        End Try
    End Sub

    Private Shared Function ContainsLegacyDrawing() As Boolean
        If Not Directory.Exists(AppPaths.DrawingsDir) Then Return False
        ' Okunamayan veya tanınmayan bir dosya varsa geçiş anahtarını koru.
        Return InspectEncryptedDrawingFormats().Any(Function(format) format <> 2)
    End Function

    Private Shared Function InspectEncryptedDrawingFormats() As List(Of Integer)
        If Not Directory.Exists(AppPaths.DrawingsDir) Then Return New List(Of Integer)()

        Return Directory.EnumerateFiles(AppPaths.DrawingsDir, "*.enc", SearchOption.AllDirectories).
            Select(AddressOf EncryptedDrawingFormat).
            ToList()
    End Function

    Private Shared Function EncryptedDrawingFormat(filePath As String) As Integer
        Try
            Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                If fs.Length < MagicV1.Length Then Return -1
                Dim prefix(MagicV1.Length - 1) As Byte
                Dim read = fs.Read(prefix, 0, prefix.Length)
                If read <> prefix.Length Then Return 0
                If StartsWithMagic(prefix, MagicV1) Then Return 1
                If StartsWithMagic(prefix, MagicV2) Then Return 2
                Return -1
            End Using
        Catch ex As Exception
            ErrorLogService.Log("CryptoService.EncryptedDrawingFormat", ex, "Path=" & If(filePath, ""))
            Return 0
        End Try
    End Function

    Private Shared Function StartsWithMagic(data As Byte(), magic As Byte()) As Boolean
        If data Is Nothing OrElse data.Length < magic.Length Then Return False
        For i As Integer = 0 To magic.Length - 1
            If data(i) <> magic(i) Then Return False
        Next
        Return True
    End Function

    Private Shared Function AcquireExclusiveLock(lockPath As String) As FileStream
        Dim directoryPath = Path.GetDirectoryName(lockPath)
        If Not String.IsNullOrWhiteSpace(directoryPath) Then Directory.CreateDirectory(directoryPath)

        For attempt As Integer = 1 To RetryCount
            Try
                Return New FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
            Catch ex As IOException
                If attempt = RetryCount Then Throw
                Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
                Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("Teknik resim şifreleme kilidi alınamadı: " & lockPath)
    End Function

    Private Shared Function ReadAllBytesShared(filePath As String) As Byte()
        For attempt As Integer = 1 To RetryCount
            Try
                Using fs As New FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite Or FileShare.Delete)
                    If fs.Length > Integer.MaxValue Then Throw New IOException("Dosya boyutu desteklenen sınırı aşıyor.")
                    If fs.Length = 0 Then Return Array.Empty(Of Byte)()
                    Dim result(CInt(fs.Length) - 1) As Byte
                    Dim totalRead As Integer = 0
                    While totalRead < result.Length
                        Dim read = fs.Read(result, totalRead, result.Length - totalRead)
                        If read = 0 Then Exit While
                        totalRead += read
                    End While
                    If totalRead <> result.Length Then Throw New EndOfStreamException("Dosya tamamen okunamadı.")
                    Return result
                End Using
            Catch ex As IOException
                If attempt = RetryCount Then Throw
                Thread.Sleep(RetryDelayMs)
            Catch ex As UnauthorizedAccessException
                If attempt = RetryCount Then Throw
                Thread.Sleep(RetryDelayMs)
            End Try
        Next

        Throw New IOException("Dosya okunamadı: " & filePath)
    End Function

    Private Shared Sub WriteAllBytesAtomic(filePath As String, data As Byte())
        Dim directoryPath = Path.GetDirectoryName(filePath)
        If Not String.IsNullOrWhiteSpace(directoryPath) Then Directory.CreateDirectory(directoryPath)

        Dim tempPath = filePath & "." & Guid.NewGuid().ToString("N") & ".tmp"
        Try
            Using fs As New FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)
                fs.Write(data, 0, data.Length)
                fs.Flush(True)
            End Using

            For attempt As Integer = 1 To RetryCount
                Try
                    File.Move(tempPath, filePath, True)
                    Return
                Catch ex As IOException
                    If attempt = RetryCount Then Throw
                    Thread.Sleep(RetryDelayMs)
                Catch ex As UnauthorizedAccessException
                    If attempt = RetryCount Then Throw
                    Thread.Sleep(RetryDelayMs)
                End Try
            Next
        Finally
            Try
                If File.Exists(tempPath) Then File.Delete(tempPath)
            Catch cleanupEx As Exception
                ErrorLogService.Log("CryptoService.WriteFileAtomic.Cleanup", cleanupEx, "Path=" & tempPath)
            End Try
        End Try
    End Sub

    Private Shared Sub AppendMigrationLog(message As String)
        Try
            Dim logPath = Path.Combine(AppPaths.DataDir, "DrawingEncryptionMigration.log")
            File.AppendAllText(
                logPath,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") & " | " & message & Environment.NewLine,
                Encoding.UTF8)
        Catch ex As Exception
            ErrorLogService.Log("CryptoService.AppendMigrationLog", ex)
        End Try
    End Sub

    Private Shared Function Combine(ParamArray arrays As Byte()()) As Byte()
        Dim totalLength = arrays.Sum(Function(value) If(value Is Nothing, 0, value.Length))
        Dim result As Byte() = If(totalLength = 0, Array.Empty(Of Byte)(), New Byte(totalLength - 1) {})
        Dim offset As Integer = 0

        For Each value In arrays
            If value Is Nothing OrElse value.Length = 0 Then Continue For
            Buffer.BlockCopy(value, 0, result, offset, value.Length)
            offset += value.Length
        Next

        Return result
    End Function
End Class
