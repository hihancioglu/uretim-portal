Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Text.Json

Public NotInheritable Class MeasurementDraftService
    Private Sub New()
    End Sub

    Private Shared ReadOnly JsonOptions As New JsonSerializerOptions With {
        .WriteIndented = True,
        .PropertyNameCaseInsensitive = True
    }

    Public Shared Function Load(userName As String, trCode As String, drawingRev As String, Optional drawingScope As String = "") As MeasurementDraft
        Dim path = GetDraftPath(userName, trCode, drawingRev, drawingScope)
        If Not File.Exists(path) Then Return Nothing

        Try
            Return JsonSerializer.Deserialize(Of MeasurementDraft)(File.ReadAllText(path, Encoding.UTF8), JsonOptions)
        Catch ex As Exception
            ErrorLogService.Log("MeasurementDraftService.Load", ex, "Path=" & path)
            Return Nothing
        End Try
    End Function

    Public Shared Sub Save(draft As MeasurementDraft)
        If draft Is Nothing Then Throw New ArgumentNullException(NameOf(draft))

        Directory.CreateDirectory(AppPaths.LocalDraftsDir)
        draft.DrawingScope = ProductInfo.NormalizeDrawingScope(draft.DrawingScope)
        Dim path = GetDraftPath(draft.UserName, draft.TrCode, draft.DrawingRev, draft.DrawingScope)
        Dim tempPath = path & "." & Guid.NewGuid().ToString("N") & ".tmp"

        Try
            File.WriteAllText(tempPath, JsonSerializer.Serialize(draft, JsonOptions), New UTF8Encoding(False))
            File.Move(tempPath, path, True)
        Finally
            If File.Exists(tempPath) Then
                Try
                    File.Delete(tempPath)
                Catch ex As Exception
                    ErrorLogService.Log("MeasurementDraftService.Save.Cleanup", ex, "Path=" & tempPath)
                End Try
            End If
        End Try
    End Sub

    Public Shared Sub Delete(userName As String, trCode As String, drawingRev As String, Optional drawingScope As String = "")
        Dim path = GetDraftPath(userName, trCode, drawingRev, drawingScope)
        If Not File.Exists(path) Then Return

        Try
            File.Delete(path)
        Catch ex As Exception
            ErrorLogService.Log("MeasurementDraftService.Delete", ex, "Path=" & path)
        End Try
    End Sub

    Private Shared Function GetDraftPath(userName As String, trCode As String, drawingRev As String, drawingScope As String) As String
        Directory.CreateDirectory(AppPaths.LocalDraftsDir)
        Dim rawKey = String.Join("|", {
            If(userName, "").Trim().ToUpperInvariant(),
            Environment.MachineName.Trim().ToUpperInvariant(),
            If(trCode, "").Trim().ToUpperInvariant(),
            If(drawingRev, "").Trim().ToUpperInvariant(),
            ProductInfo.NormalizeDrawingScope(drawingScope).ToUpperInvariant()
        })
        Dim hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).Substring(0, 24)
        Return Path.Combine(AppPaths.LocalDraftsDir, "measurement_" & hash & ".json")
    End Function
End Class
