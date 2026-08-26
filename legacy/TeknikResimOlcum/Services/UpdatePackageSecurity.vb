Imports System.IO
Imports System.IO.Compression
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Text

Public NotInheritable Class UpdatePackageSecurity
    Private Sub New()
    End Sub

    Public Const ManifestFileName As String = "_update_manifest.txt"
    Public Const SignatureFileName As String = "_update_signature.txt"

    Private Shared ReadOnly SigningModulusBase64 As String =
        "rgR0u8b8djDP9t4iD04MdFb+A1HZi66vX6gfF0zGs4/Ujd2B1uGd2YFleZ0k/spsbSzCAjKImPU5qQpdMAwoPB8rY2ikmer3sg6R+HmN/IRUBbwWnpSVNdy48GVNYRhNGNVLcW4nSPzminsJqDkwTFX3r/mMxN+MSP6wXhsRNUWMi/cY0wW/pkNJ+reFF2aAJFqtJHn5NSdolZ8BWH3OfzfAYod/QTQ5AqbEnbyi95/sUrmmojXPWo5NfnNY34DNyiWmZWM9tpDVB1wqNjXvDA3vNjcEqB3TmlESn9coqsyybA7v8Qm8GVuDNy6Zf9xBLJqTKz/cdF+BzHrPuAyKA95plFR/MwMu/RkPvkMnR8zbvvhLk6zQOddtZFuAbG9AlatKi60WymDgJtmv3W+/yvaF4viKklRaVifooL4QIvO1ZNb+q5+hkHb9E3nz8jHqK0RiElsFsQgExc9OCrX2vxdJMo6hZj43p4zvC2BLTJq1ggUZbWL1um9U2HQuZU+R"
    Private Shared ReadOnly SigningExponentBase64 As String = "AQAB"

    Private NotInheritable Class ManifestFileEntry
        Public Property RelativePath As String = ""
        Public Property Length As Long = 0
        Public Property Sha256Hex As String = ""
    End Class

    Public Shared Function ValidatePackage(packagePath As String, ByRef message As String) As Boolean
        Try
            Using archive = ZipFile.OpenRead(packagePath)
                If archive.Entries.Count = 0 Then
                    message = "Guncelleme paketi bos gorunuyor."
                    Return False
                End If

                Dim entriesByName As New Dictionary(Of String, ZipArchiveEntry)(StringComparer.OrdinalIgnoreCase)

                For Each entry In archive.Entries
                    Dim normalized = NormalizeZipName(entry.FullName)
                    If normalized = "" Then Continue For

                    Dim nameForSafety = If(IsDirectoryEntry(entry), normalized.TrimEnd("/"c), normalized)
                    If nameForSafety = "" Then Continue For

                    If Not IsSafeZipName(nameForSafety) Then
                        message = "Paket icinde guvenli olmayan dosya yolu var: " & normalized
                        Return False
                    End If

                    If IsDirectoryEntry(entry) Then Continue For

                    If entriesByName.ContainsKey(normalized) Then
                        message = "Paket icinde ayni dosya birden fazla kez bulunuyor: " & normalized
                        Return False
                    End If

                    entriesByName.Add(normalized, entry)
                Next

                If Not entriesByName.ContainsKey(ManifestFileName) Then
                    message = "Paket icinde " & ManifestFileName & " bulunamadi."
                    Return False
                End If

                If Not entriesByName.ContainsKey(SignatureFileName) Then
                    message = "Paket imza dosyasi eksik: " & SignatureFileName
                    Return False
                End If

                Dim payloadNames = entriesByName.Keys.
                    Where(Function(name) Not String.Equals(name, ManifestFileName, StringComparison.OrdinalIgnoreCase) AndAlso
                                         Not String.Equals(name, SignatureFileName, StringComparison.OrdinalIgnoreCase)).
                    ToList()

                If payloadNames.Any(Function(name) IsSourcePackagePath(name)) Then
                    message = "Secilen paket kaynak kod ZIP'i gibi gorunuyor." & Environment.NewLine &
                              "Guncelleme icin build_release_update_zip.bat ile olusturulan imzali yayin ZIP'i secilmelidir."
                    Return False
                End If

                If Not payloadNames.Any(Function(name) String.Equals(ZipFileName(name), "TeknikResimOlcum.exe", StringComparison.OrdinalIgnoreCase)) Then
                    message = "Paket icinde TeknikResimOlcum.exe bulunamadi."
                    Return False
                End If

                Dim manifestBytes = ReadEntryBytes(entriesByName(ManifestFileName))
                Dim signatureText = ReadEntryText(entriesByName(SignatureFileName))
                Dim signatureBytes As Byte() = Array.Empty(Of Byte)()
                If Not TryReadSignature(signatureText, signatureBytes) Then
                    message = "Paket imza dosyasi okunamadi veya gecersiz."
                    Return False
                End If

                If Not VerifyManifestSignature(manifestBytes, signatureBytes) Then
                    message = "Paket imzasi dogrulanamadi. Paket degistirilmis veya yetkisiz uretilmis olabilir."
                    Return False
                End If

                Dim manifestText = DecodeUtf8(manifestBytes)
                If manifestText.IndexOf("PackageType=TeknikResimOlcumUpdate", StringComparison.OrdinalIgnoreCase) < 0 OrElse
                   manifestText.IndexOf("AppName=TeknikResimOlcum", StringComparison.OrdinalIgnoreCase) < 0 Then
                    message = "Paket manifest bilgisi bu programa ait degil."
                    Return False
                End If

                Dim expectedFiles = ParseManifestFiles(manifestText)
                If expectedFiles.Count = 0 Then
                    message = "Paket manifest dosya listesi bos veya gecersiz."
                    Return False
                End If

                For Each actualName In payloadNames
                    If Not expectedFiles.ContainsKey(actualName) Then
                        message = "Paket icinde imzali manifestte olmayan dosya var: " & actualName
                        Return False
                    End If
                Next

                For Each expected In expectedFiles.Values
                    If Not entriesByName.ContainsKey(expected.RelativePath) Then
                        message = "Paket icinde manifestte beklenen dosya eksik: " & expected.RelativePath
                        Return False
                    End If

                    Dim entry = entriesByName(expected.RelativePath)
                    If entry.Length <> expected.Length Then
                        message = "Paket dosya boyutu manifest ile eslesmiyor: " & expected.RelativePath
                        Return False
                    End If

                    Dim actualHash = Sha256Hex(ReadEntryBytes(entry))
                    If Not String.Equals(actualHash, expected.Sha256Hex, StringComparison.OrdinalIgnoreCase) Then
                        message = "Paket dosya hash'i manifest ile eslesmiyor: " & expected.RelativePath
                        Return False
                    End If
                Next
            End Using

            message = "Paket imzasi ve dosya butunlugu dogrulandi."
            Return True
        Catch ex As InvalidDataException
            message = "ZIP paketi okunamadi veya bozuk gorunuyor." & Environment.NewLine & ex.Message
            Return False
        Catch ex As Exception
            message = "Guncelleme paketi dogrulanamadi." & Environment.NewLine & ex.Message
            Return False
        End Try
    End Function

    Public Shared Function TryGetValidatedBuildStamp(packagePath As String,
                                                     ByRef buildStamp As String,
                                                     ByRef message As String) As Boolean
        buildStamp = ""
        If Not ValidatePackage(packagePath, message) Then Return False

        Try
            Using archive = ZipFile.OpenRead(packagePath)
                Dim manifestEntry = archive.Entries.FirstOrDefault(
                    Function(entry) String.Equals(NormalizeZipName(entry.FullName), ManifestFileName, StringComparison.OrdinalIgnoreCase))
                If manifestEntry Is Nothing Then
                    message = "Paket manifest dosyası bulunamadı."
                    Return False
                End If

                Dim manifestText = ReadEntryText(manifestEntry)
                buildStamp = ManifestValue(manifestText, "BuildStamp")
                If String.IsNullOrWhiteSpace(buildStamp) Then
                    message = "Paket BuildStamp bilgisi içermiyor."
                    Return False
                End If
            End Using

            message = "Paket geçerli."
            Return True
        Catch ex As Exception
            message = "Paket sürüm bilgisi okunamadı. " & ex.Message
            Return False
        End Try
    End Function

    Public Shared Function ManifestValue(manifestText As String, key As String) As String
        Dim prefix = If(key, "").Trim() & "="
        If prefix = "=" Then Return ""

        For Each rawLine In If(manifestText, "").Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)
            Dim line = rawLine.Trim()
            If line.StartsWith(ChrW(&HFEFF)) Then line = line.Substring(1).Trim()
            If line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) Then
                Return line.Substring(prefix.Length).Trim()
            End If
        Next

        Return ""
    End Function

    Private Shared Function VerifyManifestSignature(manifestBytes As Byte(), signatureBytes As Byte()) As Boolean
        Using rsa As RSA = RSA.Create()
            rsa.ImportParameters(New RSAParameters With {
                .Modulus = Convert.FromBase64String(SigningModulusBase64),
                .Exponent = Convert.FromBase64String(SigningExponentBase64)
            })
            Return rsa.VerifyData(manifestBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
        End Using
    End Function

    Private Shared Function TryReadSignature(signatureText As String, ByRef signatureBytes As Byte()) As Boolean
        signatureBytes = Array.Empty(Of Byte)()
        Dim candidate = If(signatureText, "").Trim()

        For Each rawLine In candidate.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)
            Dim line = rawLine.Trim()
            If line.StartsWith("SignatureBase64=", StringComparison.OrdinalIgnoreCase) Then
                candidate = line.Substring("SignatureBase64=".Length).Trim()
                Exit For
            End If
        Next

        Try
            signatureBytes = Convert.FromBase64String(candidate)
            Return signatureBytes.Length > 0
        Catch ex As FormatException
            Return False
        End Try
    End Function

    Private Shared Function ParseManifestFiles(manifestText As String) As Dictionary(Of String, ManifestFileEntry)
        Dim result As New Dictionary(Of String, ManifestFileEntry)(StringComparer.OrdinalIgnoreCase)
        Dim inFiles As Boolean = False

        For Each rawLine In manifestText.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)
            Dim line = rawLine.Trim()
            If line.StartsWith(ChrW(&HFEFF)) Then line = line.Substring(1).Trim()
            If line = "" Then Continue For

            If line.StartsWith("[", StringComparison.Ordinal) AndAlso line.EndsWith("]", StringComparison.Ordinal) Then
                inFiles = String.Equals(line, "[Files]", StringComparison.OrdinalIgnoreCase)
                Continue For
            End If

            If Not inFiles Then Continue For

            Dim parts = line.Split("|"c)
            If parts.Length <> 3 Then Continue For

            Dim relativePath = NormalizeZipName(parts(0).Trim())
            Dim lengthValue As Long
            If relativePath = "" OrElse Not IsSafeZipName(relativePath) Then Continue For
            If Not Long.TryParse(parts(1).Trim(), lengthValue) Then Continue For

            Dim hashText = parts(2).Trim()
            If hashText.Length <> 64 OrElse Not hashText.All(Function(ch) Uri.IsHexDigit(ch)) Then Continue For

            result(relativePath) = New ManifestFileEntry With {
                .RelativePath = relativePath,
                .Length = lengthValue,
                .Sha256Hex = hashText
            }
        Next

        Return result
    End Function

    Private Shared Function IsSourcePackagePath(name As String) As Boolean
        Return name.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) OrElse
               name.StartsWith("Forms/", StringComparison.OrdinalIgnoreCase) OrElse
               name.StartsWith("Services/", StringComparison.OrdinalIgnoreCase) OrElse
               name.StartsWith("Models/", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Shared Function IsSafeZipName(name As String) As Boolean
        If String.IsNullOrWhiteSpace(name) Then Return False
        If name.StartsWith("/", StringComparison.Ordinal) OrElse name.StartsWith("\", StringComparison.Ordinal) Then Return False
        If name.Contains(":"c) Then Return False

        For Each part In name.Split("/"c)
            If part = "" OrElse part = "." OrElse part = ".." Then Return False
        Next

        Return True
    End Function

    Private Shared Function NormalizeZipName(entryName As String) As String
        Return If(entryName, "").Replace("\"c, "/"c)
    End Function

    Private Shared Function ZipFileName(entryName As String) As String
        Dim name = NormalizeZipName(entryName)
        Dim idx = name.LastIndexOf("/"c)
        If idx >= 0 Then Return name.Substring(idx + 1)
        Return name
    End Function

    Private Shared Function IsDirectoryEntry(entry As ZipArchiveEntry) As Boolean
        Return entry.FullName.EndsWith("/", StringComparison.Ordinal) OrElse entry.Name = ""
    End Function

    Private Shared Function ReadEntryBytes(entry As ZipArchiveEntry) As Byte()
        Using input = entry.Open()
            Using ms As New MemoryStream()
                input.CopyTo(ms)
                Return ms.ToArray()
            End Using
        End Using
    End Function

    Private Shared Function ReadEntryText(entry As ZipArchiveEntry) As String
        Return DecodeUtf8(ReadEntryBytes(entry))
    End Function

    Private Shared Function DecodeUtf8(bytes As Byte()) As String
        Return Encoding.UTF8.GetString(bytes)
    End Function

    Private Shared Function Sha256Hex(bytes As Byte()) As String
        Using sha As SHA256 = SHA256.Create()
            Return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "")
        End Using
    End Function
End Class
