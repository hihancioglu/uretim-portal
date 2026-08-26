Imports System.IO
Imports System.Linq

Public NotInheritable Class TestRequestAttachmentService
    Private Sub New()
    End Sub

    Private Shared ReadOnly Headers As String() = {
        "AttachmentId",
        "RequestId",
        "RelativePath",
        "OriginalFileName",
        "FileSize",
        "AddedBy",
        "AddedAt",
        "ComputerName"
    }

    Private Shared ReadOnly SupportedExtensions As HashSet(Of String) =
        New HashSet(Of String)(StringComparer.OrdinalIgnoreCase) From {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".csv", ".txt",
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff",
            ".zip", ".7z"
        }

    Private Const MaximumFileSize As Long = 50L * 1024L * 1024L

    Private Shared ReadOnly Property IndexPath As String
        Get
            Return Path.Combine(AppPaths.DataDir, "TestRequestAttachments.csv")
        End Get
    End Property

    Private Shared ReadOnly Property StorageRoot As String
        Get
            Return Path.Combine(AppPaths.DataDir, "TestRequestAttachments")
        End Get
    End Property

    Public Shared Function GetAttachments(requestId As String) As List(Of Dictionary(Of String, String))
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talebi Sonuç Dosyaları")
        Dim normalizedRequestId = If(requestId, "").Trim()
        If normalizedRequestId = "" Then Return New List(Of Dictionary(Of String, String))()

        EnsureStorage()
        Return CsvUtil.ReadRows(IndexPath).
            Where(Function(row) String.Equals(DataService.GetValue(row, "RequestId"), normalizedRequestId, StringComparison.OrdinalIgnoreCase)).
            OrderByDescending(Function(row) DataService.GetValue(row, "AddedAt")).
            ToList()
    End Function

    Public Shared Function HasAttachments(requestId As String) As Boolean
        Return GetAttachments(requestId).Count > 0
    End Function

    Public Shared Function AddAttachment(requestId As String, sourcePath As String) As Dictionary(Of String, String)
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talebi Sonuç Dosyası Ekleme")
        Dim normalizedRequestId = If(requestId, "").Trim()
        If normalizedRequestId = "" Then Throw New InvalidOperationException("Dosya eklemek için test talep numarası bulunamadı.")
        If String.IsNullOrWhiteSpace(sourcePath) OrElse Not File.Exists(sourcePath) Then
            Throw New FileNotFoundException("Seçilen dosya bulunamadı.", sourcePath)
        End If

        Dim sourceInfo As New FileInfo(sourcePath)
        If sourceInfo.Length <= 0 Then Throw New InvalidOperationException("Boş dosya yüklenemez.")
        If sourceInfo.Length > MaximumFileSize Then Throw New InvalidOperationException("Dosya boyutu 50 MB sınırını aşamaz.")

        Dim extension = sourceInfo.Extension
        If Not SupportedExtensions.Contains(extension) Then
            Throw New InvalidOperationException("Desteklenmeyen dosya biçimi. PDF, Office, CSV/TXT, görsel veya ZIP/7Z dosyası seçin.")
        End If

        EnsureStorage()
        Dim attachmentId = Guid.NewGuid().ToString("N")
        Dim requestFolder = Path.Combine(StorageRoot, SanitizeSegment(normalizedRequestId))
        Directory.CreateDirectory(requestFolder)
        Dim storedFileName = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") & "_" & attachmentId.Substring(0, 8) & extension.ToLowerInvariant()
        Dim destinationPath = Path.Combine(requestFolder, storedFileName)
        File.Copy(sourcePath, destinationPath, False)

        Dim row As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
            {"AttachmentId", attachmentId},
            {"RequestId", normalizedRequestId},
            {"RelativePath", Path.GetRelativePath(AppPaths.DataDir, destinationPath)},
            {"OriginalFileName", sourceInfo.Name},
            {"FileSize", sourceInfo.Length.ToString()},
            {"AddedBy", AppState.CurrentUserName},
            {"AddedAt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},
            {"ComputerName", Environment.MachineName}
        }

        Try
            CsvUtil.AppendRowLocked(IndexPath, Headers, row)
            AuditService.Log("TEST_REQUEST_ATTACHMENT_ADD", "", "", "Talep No=" & normalizedRequestId & "; Dosya=" & sourceInfo.Name)
            Return row
        Catch
            Try
                If File.Exists(destinationPath) Then File.Delete(destinationPath)
            Catch
            End Try
            Throw
        End Try
    End Function

    Public Shared Function DeleteAttachment(attachmentId As String, requestId As String) As Boolean
        AuthorizationService.Require(AppState.CanOpenTestRequests, "Test Talebi Sonuç Dosyası Silme")
        Dim normalizedAttachmentId = If(attachmentId, "").Trim()
        Dim normalizedRequestId = If(requestId, "").Trim()
        If normalizedAttachmentId = "" OrElse normalizedRequestId = "" Then Return False

        EnsureStorage()
        Dim deletedRow As Dictionary(Of String, String) = Nothing
        Dim removed = CsvUtil.UpdateRowsLocked(
            IndexPath,
            Headers,
            Function(rows)
                deletedRow = rows.FirstOrDefault(
                    Function(row) String.Equals(DataService.GetValue(row, "AttachmentId"), normalizedAttachmentId, StringComparison.OrdinalIgnoreCase) AndAlso
                                  String.Equals(DataService.GetValue(row, "RequestId"), normalizedRequestId, StringComparison.OrdinalIgnoreCase))
                If deletedRow Is Nothing Then Return False
                rows.Remove(deletedRow)
                Return True
            End Function)

        If removed AndAlso deletedRow IsNot Nothing Then
            Dim fullPath = ResolveAttachmentPath(deletedRow)
            If fullPath <> "" AndAlso File.Exists(fullPath) Then File.Delete(fullPath)
            AuditService.Log("TEST_REQUEST_ATTACHMENT_DELETE", "", "", "Talep No=" & normalizedRequestId & "; Dosya=" & DataService.GetValue(deletedRow, "OriginalFileName"))
        End If
        Return removed
    End Function

    Public Shared Function ResolveAttachmentPath(row As Dictionary(Of String, String)) As String
        If row Is Nothing Then Return ""
        Dim relativePath = DataService.GetValue(row, "RelativePath").Trim()
        If relativePath = "" Then Return ""

        Dim dataRoot = Path.GetFullPath(AppPaths.DataDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) & Path.DirectorySeparatorChar
        Dim candidate = Path.GetFullPath(Path.Combine(AppPaths.DataDir, relativePath))
        If Not candidate.StartsWith(dataRoot, StringComparison.OrdinalIgnoreCase) Then
            Throw New InvalidOperationException("Sonuç dosyası yolu veri klasörünün dışında olamaz.")
        End If
        Return candidate
    End Function

    Private Shared Sub EnsureStorage()
        Directory.CreateDirectory(StorageRoot)
        CsvUtil.EnsureFile(IndexPath, Headers)
    End Sub

    Private Shared Function SanitizeSegment(value As String) As String
        Dim result = value
        For Each invalidChar In Path.GetInvalidFileNameChars()
            result = result.Replace(invalidChar, "_"c)
        Next
        Return result.Trim()
    End Function
End Class
