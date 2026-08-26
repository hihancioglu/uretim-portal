Imports System.IO
Imports System.Diagnostics

Public NotInheritable Class TempFileService
    Private Sub New()
    End Sub

    Public Shared Function IsEncryptedPdf(drawingFileName As String) As Boolean
        Return If(drawingFileName, "").Trim().EndsWith(".pdf.enc", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Function IsEncryptedDxf(drawingFileName As String) As Boolean
        Return If(drawingFileName, "").Trim().EndsWith(".dxf.enc", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Function DecryptEncryptedDrawingToTemp(drawingFileName As String) As String
        If String.IsNullOrWhiteSpace(drawingFileName) Then Throw New InvalidOperationException("Teknik resim dosyası tanımlı değil.")
        Dim encPath = AppPaths.ResolveDrawingFilePath(drawingFileName)
        If Not File.Exists(encPath) Then Throw New FileNotFoundException("Şifreli teknik resim bulunamadı.", encPath)

        Dim withoutEnc = Path.GetFileNameWithoutExtension(drawingFileName)
        Dim extension = Path.GetExtension(withoutEnc)
        If String.IsNullOrWhiteSpace(extension) Then extension = ".bin"

        Dim safeBase = FileNameUtil.SafeFileName(Path.GetFileNameWithoutExtension(withoutEnc))
        If safeBase = "" Then safeBase = "drawing"
        Dim tempDrawing = Path.Combine(AppPaths.TempDir, safeBase & "_" & Guid.NewGuid().ToString("N") & extension.ToLowerInvariant())
        CryptoService.DecryptDrawing(encPath, tempDrawing)
        Return tempDrawing
    End Function

    Public Shared Function DecryptEncryptedPdfToTemp(drawingFileName As String) As String
        If Not IsEncryptedPdf(drawingFileName) Then
            Throw New InvalidOperationException(
                "Bu teknik resim PDF değil. Program içi balonlu görüntüleme için PDF teknik resim yükleyiniz." & Environment.NewLine &
                "DXF dosyaları kaynak çizim olarak saklanabilir ve harici programla açılabilir.")
        End If
        If String.IsNullOrWhiteSpace(drawingFileName) Then Throw New InvalidOperationException("Teknik resim dosyası tanımlı değil.")
        Dim encPath = AppPaths.ResolveDrawingFilePath(drawingFileName)
        If Not File.Exists(encPath) Then Throw New FileNotFoundException("Şifreli teknik resim bulunamadı.", encPath)

        Dim safeName As String = Path.GetFileNameWithoutExtension(drawingFileName).Replace(".pdf", "")
        Dim tempPdf = Path.Combine(AppPaths.TempDir, safeName & "_" & Guid.NewGuid().ToString("N") & ".pdf")
        CryptoService.DecryptPdf(encPath, tempPdf)
        Return tempPdf
    End Function

    Public Shared Sub OpenEncryptedPdf(drawingFileName As String)
        OpenEncryptedDrawing(drawingFileName)
    End Sub

    Public Shared Sub OpenEncryptedDrawing(drawingFileName As String)
        Dim viewer As New FrmPdfViewer(drawingFileName, "Teknik Resim")
        viewer.ShowDialog()
    End Sub

    Public Shared Sub TryDeleteTempPdf(tempPdf As String)
        Try
            If Not String.IsNullOrWhiteSpace(tempPdf) AndAlso File.Exists(tempPdf) Then
                File.Delete(tempPdf)
            End If
        Catch ex As Exception
            ErrorLogService.Log("TempFileService.TryDeleteTempPdf", ex, "Path=" & If(tempPdf, ""))
        End Try
    End Sub

    Public Shared Sub CleanTempFiles()
        Try
            If Not Directory.Exists(AppPaths.TempDir) Then Return
            For Each tempFile As String In Directory.GetFiles(AppPaths.TempDir, "*.pdf")
                Try
                    File.Delete(tempFile)
                Catch ex As Exception
                    ErrorLogService.Log("TempFileService.CleanTempFiles.Pdf", ex, "Path=" & tempFile)
                End Try
            Next

            For Each tempFile As String In Directory.GetFiles(AppPaths.TempDir, "*.png")
                Try
                    File.Delete(tempFile)
                Catch ex As Exception
                    ErrorLogService.Log("TempFileService.CleanTempFiles.Png", ex, "Path=" & tempFile)
                End Try
            Next

            For Each tempFile As String In Directory.GetFiles(AppPaths.TempDir, "*.dxf")
                Try
                    File.Delete(tempFile)
                Catch ex As Exception
                    ErrorLogService.Log("TempFileService.CleanTempFiles.Dxf", ex, "Path=" & tempFile)
                End Try
            Next

            For Each tempFile As String In Directory.GetFiles(AppPaths.TempDir, "*.svg")
                Try
                    File.Delete(tempFile)
                Catch ex As Exception
                    ErrorLogService.Log("TempFileService.CleanTempFiles.Svg", ex, "Path=" & tempFile)
                End Try
            Next
        Catch ex As Exception
            ErrorLogService.Log("TempFileService.CleanTempFiles", ex)
        End Try
    End Sub
End Class
