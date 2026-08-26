Imports System.IO
Imports System.Linq

Public NotInheritable Class ShiftTrackingPhotoService
    Private Sub New()
    End Sub

    Private Shared ReadOnly Headers As String() = {
        "PhotoId",
        "RecordId",
        "ModuleType",
        "RelativePath",
        "OriginalFileName",
        "AddedBy",
        "AddedAt",
        "ComputerName"
    }

    Private Shared ReadOnly SupportedExtensions As HashSet(Of String) =
        New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        }

    Private Shared ReadOnly Property IndexPath As String
        Get
            Return Path.Combine(AppPaths.DataDir, "ShiftTrackingPhotos.csv")
        End Get
    End Property

    Private Shared ReadOnly Property StorageRoot As String
        Get
            Return Path.Combine(AppPaths.DataDir, "ShiftTrackingPhotos")
        End Get
    End Property

    Public Shared Function GetPhotos(recordId As String, mechanismMode As Boolean) As List(Of Dictionary(Of String, String))
        Dim normalizedRecordId = If(recordId, "").Trim()
        If normalizedRecordId = "" Then Return New List(Of Dictionary(Of String, String))()

        Directory.CreateDirectory(StorageRoot)
        CsvUtil.EnsureFile(IndexPath, Headers)
        Dim moduleType = GetModuleType(mechanismMode)
        Return CsvUtil.ReadRows(IndexPath).
            Where(Function(row) String.Equals(DataService.GetValue(row, "RecordId"), normalizedRecordId, StringComparison.OrdinalIgnoreCase) AndAlso
                                String.Equals(DataService.GetValue(row, "ModuleType"), moduleType, StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(row) DataService.GetValue(row, "AddedAt")).
            ToList()
    End Function

    Public Shared Function AddPhoto(recordId As String, mechanismMode As Boolean, sourcePath As String) As Dictionary(Of String, String)
        Dim normalizedRecordId = If(recordId, "").Trim()
        If normalizedRecordId = "" Then Throw New InvalidOperationException("Fotoğraf eklemek için kayıt numarası bulunamadı.")
        If String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then
            Throw New FileNotFoundException("Seçilen fotoğraf bulunamadı.", sourcePath)
        End If

        Dim extension = Path.GetExtension(sourcePath)
        If Not SupportedExtensions.Contains(extension) Then
            Throw New InvalidOperationException("Desteklenmeyen fotoğraf biçimi. JPG, JPEG, PNG, BMP veya GIF seçin.")
        End If

        Dim moduleType = GetModuleType(mechanismMode)
        Dim photoId = Guid.NewGuid().ToString("N")
        Dim recordFolder = Path.Combine(StorageRoot, moduleType, SanitizeSegment(normalizedRecordId))
        Directory.CreateDirectory(recordFolder)

        Dim storedFileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") & "_" & photoId.Substring(0, 8) & extension.ToLowerInvariant()
        Dim destinationPath = Path.Combine(recordFolder, storedFileName)
        File.Copy(sourcePath, destinationPath, False)

        Dim relativePath = Path.GetRelativePath(AppPaths.DataDir, destinationPath)
        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"PhotoId", photoId},
            {"RecordId", normalizedRecordId},
            {"ModuleType", moduleType},
            {"RelativePath", relativePath},
            {"OriginalFileName", Path.GetFileName(sourcePath)},
            {"AddedBy", AppState.CurrentUserName},
            {"AddedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"ComputerName", Environment.MachineName}
        }

        Try
            CsvUtil.AppendRowLocked(IndexPath, Headers, row)
            Return row
        Catch
            Try
                If File.Exists(destinationPath) Then File.Delete(destinationPath)
            Catch
            End Try
            Throw
        End Try
    End Function

    Public Shared Function DeletePhoto(photoId As String, recordId As String, mechanismMode As Boolean) As Boolean
        Dim normalizedPhotoId = If(photoId, "").Trim()
        Dim normalizedRecordId = If(recordId, "").Trim()
        If normalizedPhotoId = "" OrElse normalizedRecordId = "" Then Return False

        Dim moduleType = GetModuleType(mechanismMode)
        Dim deletedRow As Dictionary(Of String, String) = Nothing
        Dim removed = CsvUtil.UpdateRowsLocked(
            IndexPath,
            Headers,
            Function(rows)
                deletedRow = rows.FirstOrDefault(
                    Function(row) String.Equals(DataService.GetValue(row, "PhotoId"), normalizedPhotoId, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(DataService.GetValue(row, "RecordId"), normalizedRecordId, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(DataService.GetValue(row, "ModuleType"), moduleType, StringComparison.OrdinalIgnoreCase))
                If deletedRow Is Nothing Then Return False
                rows.Remove(deletedRow)
                Return True
            End Function)

        If removed AndAlso deletedRow IsNot Nothing Then
            Dim fullPath = ResolvePhotoPath(deletedRow)
            Try
                If fullPath <> "" AndAlso File.Exists(fullPath) Then File.Delete(fullPath)
            Catch ex As Exception
                ErrorLogService.Log("ShiftTrackingPhotoService.DeletePhotoFile", ex, fullPath)
            End Try
        End If
        Return removed
    End Function

    Public Shared Function ResolvePhotoPath(row As Dictionary(Of String, String)) As String
        If row Is Nothing Then Return ""
        Dim relativePath = DataService.GetValue(row, "RelativePath").Trim()
        If relativePath = "" Then Return ""

        Dim dataRoot = Path.GetFullPath(AppPaths.DataDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim candidate = Path.GetFullPath(Path.Combine(AppPaths.DataDir, relativePath))
        If Not candidate.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Fotoğraf yolu veri klasörünün dışında olamaz.")
        End If
        Return candidate
    End Function

    Private Shared Function GetModuleType(mechanismMode As Boolean) As String
        Return If(mechanismMode, "MECHANISM", "PLASTIC")
    End Function

    Private Shared Function SanitizeSegment(value As String) As String
        Dim result = value
        For Each invalidChar In Path.GetInvalidFileNameChars()
            result = result.Replace(invalidChar, "_"c)
        Next
        Return result.Trim()
    End Function
End Class
